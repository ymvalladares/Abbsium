using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Server.Chat.HubFolder;
using Server.Data;
using Server.Entitys;
using Server.Services;

namespace Server.Services
{
    public class ClippingService : IClippingService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IS3Service _s3Service;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHubContext<ClippingHub> _hubContext;
        private readonly ILogger<ClippingService> _logger;
        private readonly string _tempDir;
        private readonly string _ytDlpPath;
        private readonly string _ffmpegPath;
        private readonly string _clipsDir;
        private bool _useNvenc;
        private readonly int _maxParallelClips;
        private static SemaphoreSlim? _jobQueue;
        private static readonly object _queueLock = new();
        private static bool? _nvencAvailable;
        private static readonly object _nvencLock = new();

        public ClippingService(
            IServiceScopeFactory scopeFactory,
            IS3Service s3Service,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            IHubContext<ClippingHub> hubContext,
            ILogger<ClippingService> logger,
            IWebHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _s3Service = s3Service;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _hubContext = hubContext;
            _logger = logger;
            
            _clipsDir = Path.Combine(env.WebRootPath, "clips");
            Directory.CreateDirectory(_clipsDir);

            _tempDir = Path.Combine(Path.GetTempPath(), "abbsium_clips");
            Directory.CreateDirectory(_tempDir);

            _ytDlpPath = FindExecutable("yt-dlp.exe", new[]
            {
                @"C:\Users\yorda\AppData\Local\Microsoft\WinGet\Packages\yt-dlp.yt-dlp_Microsoft.Winget.Source_8wekyb3d8bbwe\yt-dlp.exe",
                @"C:\ProgramData\winget\yt-dlp\yt-dlp.exe"
            });

            _ffmpegPath = FindExecutable("ffmpeg.exe", new[]
            {
                @"C:\Users\yorda\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0-full_build\bin\ffmpeg.exe",
                @"C:\Users\yorda\AppData\Local\Microsoft\WinGet\Packages\yt-dlp.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-N-125875-g5d4d3bdc61-win64-gpl\bin\ffmpeg.exe",
                @"C:\ProgramData\winget\FFmpeg\ffmpeg.exe"
            });

            _useNvenc = DetectNvencCached(_ffmpegPath);
            _maxParallelClips = _config.GetValue<int>("Clipping:MaxParallelClips");
            if (_maxParallelClips <= 0) _maxParallelClips = 3;
            EnsureJobQueueInitialized(_config);

            _logger.LogInformation("ClippingService initialized: clipsDir={ClipsDir}, yt-dlp={YtDlp}, ffmpeg={Ffmpeg}, nvenc={Nvenc}, maxParallelClips={MaxParallelClips}", _clipsDir, _ytDlpPath ?? "NOT FOUND", _ffmpegPath ?? "NOT FOUND", _useNvenc, _maxParallelClips);
        }

        private static bool DetectNvencCached(string ffmpegPath)
        {
            lock (_nvencLock)
            {
                if (!_nvencAvailable.HasValue)
                    _nvencAvailable = DetectNvenc(ffmpegPath);
                return _nvencAvailable.Value;
            }
        }

        private static bool DetectNvenc(string ffmpegPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-hide_banner -encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                return output.Contains("h264_nvenc", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void EnsureJobQueueInitialized(IConfiguration config)
        {
            if (_jobQueue != null) return;

            lock (_queueLock)
            {
                if (_jobQueue == null)
                {
                    var maxConcurrent = config.GetValue<int>("Clipping:MaxConcurrentJobs");
                    if (maxConcurrent <= 0) maxConcurrent = 3;
                    _jobQueue = new SemaphoreSlim(maxConcurrent, maxConcurrent);
                }
            }
        }

        private async Task<bool> IsH264VideoAsync(string videoPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = $"-hide_banner -i \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = await process.StandardError.ReadToEndAsync();
                await process.WaitForAsync(TimeSpan.FromSeconds(30));

                var videoLine = output.Split('\n').FirstOrDefault(l => l.Contains("Video:", StringComparison.OrdinalIgnoreCase));
                return videoLine != null
                    && (videoLine.Contains("h264", StringComparison.OrdinalIgnoreCase)
                        || videoLine.Contains("avc1", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private static string FindExecutable(string name, string[] knownPaths)
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var path in knownPaths)
                {
                    if (File.Exists(path)) return path;
                }
            }

            var plainName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;

            if (!OperatingSystem.IsWindows())
            {
                var linuxPaths = new[] { "/usr/local/bin", "/usr/bin", "/bin", "/usr/sbin", "/snap/bin", "/opt/yt-dlp" };
                foreach (var dir in linuxPaths)
                {
                    var fullPath = Path.Combine(dir, plainName);
                    if (File.Exists(fullPath)) return fullPath;
                }
            }

            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                foreach (var dir in envPath.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(dir.Trim(), name);
                    if (File.Exists(fullPath)) return fullPath;

                    if (!OperatingSystem.IsWindows())
                    {
                        var plainPath = Path.Combine(dir.Trim(), plainName);
                        if (File.Exists(plainPath)) return plainPath;
                    }
                }
            }

            return OperatingSystem.IsWindows() ? name : plainName;
        }

        public async Task<ClipJob> CreateJobAsync(string userId, string videoUrl, int maxClips, int minDuration, int maxDuration)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var job = new ClipJob
            {
                UserId = userId,
                VideoUrl = videoUrl,
                MaxClips = maxClips,
                MinClipDuration = minDuration,
                MaxClipDuration = maxDuration,
                Status = "pending",
                Progress = 0
            };

            context.ClipJobs.Add(job);
            await context.SaveChangesAsync();

            _ = Task.Run(async () => await ProcessJobAsync(job.Id));

            return job;
        }

        public async Task<ClipJob?> GetJobAsync(string userId, Guid jobId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            return await context.ClipJobs
                .Include(j => j.Clips)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);
        }

        public async Task<List<ClipJob>> GetUserJobsAsync(string userId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            return await context.ClipJobs
                .Include(j => j.Clips)
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();
        }

        public async Task ProcessJobAsync(Guid jobId)
        {
            EnsureJobQueueInitialized(_config);
            await _jobQueue!.WaitAsync();
            try
            {
                await ProcessJobCoreAsync(jobId);
            }
            finally
            {
                _jobQueue.Release();
            }
        }

        private async Task ProcessJobCoreAsync(Guid jobId)
        {
            _logger.LogInformation("=== Starting processing for job {JobId} ===", jobId);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var job = await context.ClipJobs.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogError("Job {JobId} not found", jobId);
                return;
            }

            _logger.LogInformation("Job {JobId}: Found job, status={Status}", jobId, job.Status);

            try
            {
                await UpdateProgress(job, "downloading", 5, "Downloading audio...");
                _logger.LogInformation("Job {JobId}: Starting audio download from {Url}", jobId, job.VideoUrl);

                var audioFile = await DownloadAudioOnly(job.VideoUrl, jobId, job);

                var preparedAudio = await PrepareAudioForTranscription(audioFile, job);
                if (preparedAudio != audioFile)
                {
                    await UpdateProgress(job, "extracting_audio", 15, "Preparing audio for transcription...");
                }

                await UpdateProgress(job, "uploading_audio", 20, "Uploading audio for transcription...");

                var transcriptResult = await TranscribeWithAssemblyAI(preparedAudio, job);

                await UpdateProgress(job, "analyzing", 60, "Finding best moments with AI...");

                var highlights = await FindHighlightsWithGPT(transcriptResult.Text, job);

                await UpdateProgress(job, "generating_clips", 70, $"Downloading {highlights.Count} clip segments...");

                var (segmentPaths, videoPath) = await DownloadVideoSourceAsync(job.VideoUrl, jobId, job, highlights);

                await GenerateClips(segmentPaths, videoPath, highlights, job);

                await UpdateProgress(job, "completed", 100, "All clips ready!");

                job.Status = "completed";
                job.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();

                await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobCompleted", job.Id);
                _logger.LogInformation("=== Job {JobId} completed successfully ===", jobId);

                CleanupTempFiles(jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR processing clip job {JobId}: {Message} ===", jobId, ex.Message);
                await UpdateProgress(job, "failed", 0, ex.Message);
                await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobFailed", jobId, ex.Message);
                CleanupTempFiles(jobId);
            }
        }

        private async Task UpdateProgress(ClipJob job, string status, int progress, string? message = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var current = await context.ClipJobs.FindAsync(job.Id);
            if (current == null) return;

            current.Status = status;
            current.Progress = progress;
            await context.SaveChangesAsync();

            var payload = new { jobId = job.Id, status, progress, message };
            await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobProgress", payload);
            _logger.LogInformation("Job {JobId}: {Status} - {Progress}% - {Message}", job.Id, status, progress, message);
        }

        private async Task<string> DownloadAudioOnly(string videoUrl, Guid jobId, ClipJob job)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            _logger.LogInformation("Job {JobId}: Running yt-dlp (audio only) for URL: {Url}", jobId, videoUrl);

            var outputTemplate = Path.Combine(jobDir, "audio.%(ext)s");
            var ytDlpArgs = $"--no-playlist -f \"bestaudio[ext=m4a]/bestaudio\" {cookiesArg}-o \"{outputTemplate}\" \"{videoUrl}\"";
            var ytDlpResult = await RunProcess(_ytDlpPath, ytDlpArgs);

            var audioPath = FindDownloadedFile(jobDir, "audio");
            if (audioPath == null)
            {
                _logger.LogWarning("Job {JobId}: Audio-only download failed, trying fallback format...", jobId);
                var fallbackArgs = $"--no-playlist -f best {cookiesArg}-o \"{outputTemplate}\" \"{videoUrl}\"";
                ytDlpResult = await RunProcess(_ytDlpPath, fallbackArgs);
                audioPath = FindDownloadedFile(jobDir, "audio");
            }

            if (audioPath == null)
            {
                throw new Exception($"yt-dlp failed to download audio. Error: {ytDlpResult.Error.Substring(0, Math.Min(500, ytDlpResult.Error.Length))}");
            }

            var fileSize = new FileInfo(audioPath).Length;
            _logger.LogInformation("Job {JobId}: Audio downloaded successfully ({File}). Size: {Size} bytes", jobId, Path.GetFileName(audioPath), fileSize);
            return audioPath;
        }

        private static string? FindDownloadedFile(string dir, string namePrefix)
        {
            return Directory.GetFiles(dir, $"{namePrefix}.*")
                .FirstOrDefault(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                                  && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                                  && new FileInfo(f).Length > 0);
        }

        private async Task<string> PrepareAudioForTranscription(string audioPath, ClipJob job)
        {
            var ext = Path.GetExtension(audioPath).ToLowerInvariant();
            var readyExtensions = new[] { ".m4a", ".mp3", ".wav", ".flac", ".aac" };
            if (readyExtensions.Contains(ext))
            {
                _logger.LogInformation("Job {JobId}: Audio file {File} is transcription-ready ({Ext})", job.Id, Path.GetFileName(audioPath), ext);
                return audioPath;
            }

            _logger.LogInformation("Job {JobId}: Converting audio ({Ext}) to MP3 for transcription...", job.Id, ext);

            var mp3Path = Path.ChangeExtension(audioPath, ".mp3");
            var ffmpegAudioArgs = $"-i \"{audioPath}\" -vn -acodec libmp3lame -ab 128k -ar 44100 -y \"{mp3Path}\"";
            var audioResult = await RunProcess(_ffmpegPath, ffmpegAudioArgs);

            if (!audioResult.Success || !File.Exists(mp3Path))
            {
                var errorMsg = audioResult.Error.Length > 500 ? audioResult.Error.Substring(0, 500) : audioResult.Error;
                throw new Exception($"Failed to prepare audio for transcription: {errorMsg}");
            }

            var audioSize = new FileInfo(mp3Path).Length;
            _logger.LogInformation("Job {JobId}: Audio converted to MP3. Size: {Size} bytes", job.Id, audioSize);
            return mp3Path;
        }

        private async Task<List<string?>> DownloadVideoSegments(string videoUrl, Guid jobId, ClipJob job, List<ClipSegment> highlights)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            var segmentPaths = new string?[highlights.Count];
            var semaphore = new SemaphoreSlim(3, 3);
            var tasks = new List<Task>();

            for (int i = 0; i < highlights.Count; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var segment = highlights[index];
                        var segmentPath = Path.Combine(jobDir, $"segment_{index + 1}.mp4");
                        var args = $"--no-playlist --download-sections \"*{segment.StartTime}-{segment.EndTime}\" -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best\" -S res:1080 {cookiesArg}-o \"{segmentPath}\" \"{videoUrl}\"";
                        _logger.LogInformation("Job {JobId}: Downloading segment {Index} ({Start}s-{End}s): {Args}", jobId, index + 1, segment.StartTime, segment.EndTime, args);
                        var result = await RunProcess(_ytDlpPath, args);

                        if (result.Success && File.Exists(segmentPath) && new FileInfo(segmentPath).Length > 1024)
                        {
                            segmentPaths[index] = segmentPath;
                            _logger.LogInformation("Job {JobId}: Segment {Index} downloaded, size={Size} bytes", jobId, index + 1, new FileInfo(segmentPath).Length);
                        }
                        else
                        {
                            var err = result.Error.Length > 300 ? result.Error.Substring(0, 300) : result.Error;
                            _logger.LogWarning("Job {JobId}: Segment {Index} download failed: {Error}", jobId, index + 1, err);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return segmentPaths.ToList();
        }

        private async Task<string> DownloadFullVideo(string videoUrl, Guid jobId, ClipJob job)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var videoPath = Path.Combine(jobDir, "video.mp4");

            _logger.LogInformation("Job {JobId}: Running yt-dlp (full video) for URL: {Url}", jobId, videoUrl);

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            var ytDlpArgs = $"--no-playlist -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best\" -S res:1080 {cookiesArg}-o \"{videoPath}\" \"{videoUrl}\"";
            var ytDlpResult = await RunProcess(_ytDlpPath, ytDlpArgs);

            if (!File.Exists(videoPath))
            {
                _logger.LogWarning("Job {JobId}: First attempt failed, trying fallback...", jobId);
                var fallbackArgs = $"-f best {cookiesArg}-o \"{videoPath}\" \"{videoUrl}\"";
                ytDlpResult = await RunProcess(_ytDlpPath, fallbackArgs);
            }

            if (!File.Exists(videoPath))
            {
                throw new Exception($"yt-dlp failed to download video. Error: {ytDlpResult.Error.Substring(0, Math.Min(500, ytDlpResult.Error.Length))}");
            }

            var fileSize = new FileInfo(videoPath).Length;
            if (fileSize < 1024)
                throw new Exception($"Source video is too small ({fileSize} bytes). Download likely failed.");

            _logger.LogInformation("Job {JobId}: Video downloaded successfully. Size: {Size} bytes", jobId, fileSize);
            return videoPath;
        }

        private async Task<(List<string>? SegmentPaths, string? VideoPath)> DownloadVideoSourceAsync(string videoUrl, Guid jobId, ClipJob job, List<ClipSegment> highlights)
        {
            var clipCount = Math.Min(highlights.Count, job.MaxClips);
            var segments = await DownloadVideoSegments(videoUrl, jobId, job, highlights.Take(clipCount).ToList());

            if (segments.All(s => !string.IsNullOrEmpty(s)))
            {
                _logger.LogInformation("Job {JobId}: All {Count} clip segments downloaded successfully", jobId, segments.Count);
                return (segments.Select(s => s!).ToList(), null);
            }

            var okCount = segments.Count(s => !string.IsNullOrEmpty(s));
            _logger.LogWarning("Job {JobId}: Segment download incomplete ({OkCount}/{Total}), falling back to full video download...", jobId, okCount, segments.Count);
            var videoPath = await DownloadFullVideo(videoUrl, jobId, job);
            return (null, videoPath);
        }

        private async Task<(bool Success, string Output, string Error)> RunProcess(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) throw new Exception($"Could not start process: {fileName}");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForAsync(TimeSpan.FromMinutes(30));

            return (process.ExitCode == 0, await outputTask, await errorTask);
        }

        private async Task<(bool Success, string Output, string Error)> RunProcessWithProgress(string fileName, string arguments, ClipJob job, int startProgress, int endProgress, string label)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) throw new Exception($"Could not start process: {fileName}");

            var progressTask = Task.Run(async () =>
            {
                var currentProgress = startProgress;
                while (!process.HasExited)
                {
                    currentProgress = Math.Min(currentProgress + 1, endProgress - 1);
                    await UpdateProgress(job, job.Status, currentProgress, label);
                    await Task.Delay(1000);
                }
            });

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForAsync(TimeSpan.FromMinutes(30));

            await progressTask;

            return (process.ExitCode == 0, await outputTask, await errorTask);
        }

        private async Task<(string Text, string TranscriptId)> TranscribeWithAssemblyAI(string audioPath, ClipJob job)
        {
            var client = _httpClientFactory.CreateClient();
            var assemblyKey = _config["AssemblyAI:ApiKey"];
            if (string.IsNullOrEmpty(assemblyKey))
                throw new Exception("AssemblyAI API key not configured");

            client.DefaultRequestHeaders.Add("authorization", assemblyKey);

            var uploadUrl = await UploadAudioToAssemblyAI(client, audioPath);

            var transcriptRequest = new
            {
                audio_url = uploadUrl
            };

            var content = new StringContent(
                JsonSerializer.Serialize(transcriptRequest),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("https://api.assemblyai.com/v2/transcript", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var transcriptId = result.GetProperty("id").GetString() ?? throw new Exception("No transcript ID");

            var completedTranscript = await PollTranscriptStatus(client, transcriptId, job);

            var text = completedTranscript.GetProperty("text").GetString() ?? string.Empty;
            return (text, transcriptId);
        }

        private async Task<string> UploadAudioToAssemblyAI(HttpClient client, string audioPath)
        {
            using var fileStream = File.OpenRead(audioPath);
            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await client.PostAsync("https://api.assemblyai.com/v2/upload", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("upload_url").GetString() ?? throw new Exception("No upload URL");
        }

        private async Task<JsonElement> PollTranscriptStatus(HttpClient client, string transcriptId, ClipJob job)
        {
            var maxAttempts = 120;
            var delay = TimeSpan.FromSeconds(2);

            for (int i = 0; i < maxAttempts; i++)
            {
                var response = await client.GetAsync($"https://api.assemblyai.com/v2/transcript/{transcriptId}");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var status = result.GetProperty("status").GetString();

                var progress = 25 + (int)((i / (double)maxAttempts) * 30);
                await UpdateProgress(job, "transcribing", progress, $"Transcribing... ({Math.Round((i / (double)maxAttempts) * 100)}%)");

                if (status == "completed")
                    return result;

                if (status == "error")
                    throw new Exception($"AssemblyAI transcription failed: {result.GetProperty("error").GetString()}");

                await Task.Delay(delay);
            }

            throw new Exception("Transcription timed out");
        }

        private async Task<List<ClipSegment>> FindHighlightsWithGPT(string transcript, ClipJob job)
        {
            await UpdateProgress(job, "analyzing", 60, "Analyzing transcript with AI...");

            var apiKey = _config["OpenAI:ApiKey"];
            var client = new ChatClient("gpt-4o-mini", apiKey);

            var systemPrompt = @"You are an expert video content analyst. Your job is to find the most engaging, viral-worthy segments from a video transcript.

Return ONLY a JSON array of clip segments. Each segment must have:
- ""title"": A catchy title for the clip
- ""description"": Brief description of what happens
- ""start_time"": Start time in seconds (MUST be different for each clip)
- ""end_time"": End time in seconds (MUST be at least 30 seconds after start_time)
- ""engagement_score"": Score from 0-100 based on how viral-worthy it is

CRITICAL RULES:
- Each clip MUST cover a DIFFERENT part of the video. Do NOT return the same timestamps.
- Clips should be 30-60 seconds each.
- Spread clips across the entire video duration. First clip at the beginning, last clip near the end.
- NEVER return start_time=0 for all clips. Distribute them: e.g., 0-45, 120-165, 300-345.
- Return maximum 5 clips.
- Return ONLY the JSON array, no other text";

            var userMessage = $"Here is the transcript:\n\n{transcript}\n\nFind the most engaging clips from this transcript.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            await UpdateProgress(job, "analyzing", 65, "AI is finding the best moments...");

            var response = await client.CompleteChatAsync(messages);
            var responseContent = response.Value.Content
                .FirstOrDefault(c => c.Kind == ChatMessageContentPartKind.Text)?
                .Text ?? throw new Exception("No response from GPT");

            responseContent = responseContent.Trim();
            if (responseContent.StartsWith("```"))
            {
                var lines = responseContent.Split('\n');
                responseContent = string.Join('\n', lines.Skip(1).Take(lines.Length - 2));
            }

            _logger.LogInformation("Job {JobId}: GPT raw response: {Response}", job.Id, responseContent.Substring(0, Math.Min(500, responseContent.Length)));

            var segments = JsonSerializer.Deserialize<List<ClipSegment>>(responseContent) ?? new List<ClipSegment>();

            _logger.LogInformation("Job {JobId}: Deserialized {Count} segments", job.Id, segments.Count);
            
            var allZero = segments.All(s => s.StartTime == 0 && s.EndTime == 0);
            if (allZero && segments.Count > 0)
            {
                _logger.LogWarning("Job {JobId}: GPT returned all zero timestamps. Distributing clips evenly.", job.Id);
                var clipDuration = 45;
                var spacing = 120;
                for (int i = 0; i < segments.Count; i++)
                {
                    segments[i].StartTime = i * spacing;
                    segments[i].EndTime = segments[i].StartTime + clipDuration;
                    _logger.LogInformation("Job {JobId}: Fixed segment {Index} to {Start}s-{End}s", job.Id, i + 1, segments[i].StartTime, segments[i].EndTime);
                }
            }
            else
            {
                foreach (var seg in segments)
                {
                    _logger.LogInformation("Job {JobId}: Segment '{Title}' start={Start}s end={End}s", job.Id, seg.Title, seg.StartTime, seg.EndTime);
                    if (seg.EndTime <= seg.StartTime)
                    {
                        seg.EndTime = seg.StartTime + 45;
                        _logger.LogInformation("Job {JobId}: Fixed segment '{Title}' duration from 0 to 45s", job.Id, seg.Title);
                    }
                }
            }

            return segments;
        }

        private async Task GenerateClips(List<string>? segmentPaths, string? videoPath, List<ClipSegment> highlights, ClipJob job)
        {
            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            Directory.CreateDirectory(jobDir);
            var totalClips = Math.Min(highlights.Count, job.MaxClips);

            if (totalClips <= 0)
            {
                _logger.LogWarning("Job {JobId}: No clips to generate", job.Id);
                return;
            }

            if (videoPath != null && !File.Exists(videoPath))
                throw new Exception($"Source video not found: {videoPath}");

            _logger.LogInformation("Job {JobId}: Generating {Total} clips from {Source}", job.Id, totalClips, videoPath ?? "segments");

            var maxParallel = Math.Max(1, Math.Min(_maxParallelClips, totalClips));
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            var tasks = new List<Task>();

            for (int i = 0; i < totalClips; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await GenerateSingleClipAsync(segmentPaths?[index], videoPath, highlights[index], job, index, totalClips, jobDir);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private async Task GenerateSingleClipAsync(string? segmentPath, string? videoPath, ClipSegment segment, ClipJob job, int index, int totalClips, string jobDir)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var clipPath = Path.Combine(jobDir, $"clip_{index + 1}.mp4");
            var thumbnailPath = Path.Combine(jobDir, $"thumb_{index + 1}.jpg");

            var duration = segment.EndTime - segment.StartTime;
            var clipProgress = 80 + (int)((index / (double)totalClips) * 20);
            await UpdateProgress(job, "generating_clips", clipProgress, $"Generating clip {index + 1}/{totalClips}...");

            var sourcePath = segmentPath ?? videoPath!;
            var useStreamCopy = await IsH264VideoAsync(sourcePath);

            (bool Success, string Output, string Error) result = (false, string.Empty, string.Empty);

            if (segmentPath != null)
            {
                if (useStreamCopy)
                {
                    var remuxArgs = $"-i \"{segmentPath}\" -c copy -movflags +faststart -y \"{clipPath}\"";
                    _logger.LogInformation("Job {JobId}: Remuxing segment for clip {Index}: {Args}", job.Id, index + 1, remuxArgs);
                    result = await RunProcess(_ffmpegPath, remuxArgs);

                    if (!result.Success)
                    {
                        var err = result.Error.Length > 300 ? result.Error.Substring(0, 300) : result.Error;
                        _logger.LogWarning("Job {JobId}: Remux failed for clip {Index}, using segment file directly: {Error}", job.Id, index + 1, err);
                        File.Copy(segmentPath, clipPath, true);
                    }
                }
                else
                {
                    _logger.LogInformation("Job {JobId}: Segment {Index} is not H.264, re-encoding...", job.Id, index + 1);
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath));

                    if (!result.Success && _useNvenc)
                    {
                        _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                        _useNvenc = false;
                        result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath));
                    }
                }
            }
            else if (useStreamCopy)
            {
                var copyArgs = $"-ss {segment.StartTime} -i \"{videoPath}\" -t {duration} -c:v copy -c:a copy -movflags +faststart -y \"{clipPath}\"";
                _logger.LogInformation("Job {JobId}: Running FFmpeg (stream copy) for clip {Index}: {Args}", job.Id, index + 1, copyArgs);
                result = await RunProcess(_ffmpegPath, copyArgs);

                if (!result.Success)
                {
                    _logger.LogWarning("Job {JobId}: Stream copy failed for clip {Index}, retrying with re-encode: {Error}", job.Id, index + 1, result.Error.Substring(0, Math.Min(300, result.Error.Length)));
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath));

                    if (!result.Success && _useNvenc)
                    {
                        _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                        _useNvenc = false;
                        result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath));
                    }
                }
            }
            else
            {
                _logger.LogInformation("Job {JobId}: Full video is not H.264, re-encoding clip {Index}...", job.Id, index + 1);
                result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath));

                if (!result.Success && _useNvenc)
                {
                    _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                    _useNvenc = false;
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath));
                }
            }

            var clipSize = File.Exists(clipPath) ? new FileInfo(clipPath).Length : 0;
            _logger.LogInformation("Job {JobId}: Clip {Index} generated, size={Size} bytes", job.Id, index + 1, clipSize);

            if (clipSize < 1024)
            {
                _logger.LogWarning("Job {JobId}: Clip {Index} is too small ({Size} bytes), skipping", job.Id, index + 1, clipSize);
                return;
            }

            var thumbArgs = $"-ss {duration / 2} -i \"{clipPath}\" -vframes 1 -q:v 2 -y \"{thumbnailPath}\"";
            await RunProcess(_ffmpegPath, thumbArgs);

            var clip = new VideoClip
            {
                ClipJobId = job.Id,
                Title = segment.Title,
                Description = segment.Description,
                StartTimeSeconds = segment.StartTime,
                EndTimeSeconds = segment.EndTime,
                DurationSeconds = duration,
                S3Key = $"/clips/{job.Id}/clip_{index + 1}.mp4",
                ThumbnailS3Key = $"/clips/{job.Id}/thumb_{index + 1}.jpg",
                EngagementScore = segment.EngagementScore
            };

            context.VideoClips.Add(clip);
            await context.SaveChangesAsync();

            _logger.LogInformation("Job {JobId}: Clip {Index} saved to DB", job.Id, index + 1);
        }

        private string BuildClipFfmpegArgs(int startTime, int duration, string videoPath, string clipPath)
        {
            var encoderArgs = _useNvenc
                ? "-c:v h264_nvenc -preset p4 -rc vbr -cq 19 -b:v 0 -profile:v high -level 4.1 -pix_fmt yuv420p"
                : "-c:v libx264 -preset veryfast -crf 16 -profile:v high -level 4.1 -pix_fmt yuv420p";

            return $"-ss {startTime} -i \"{videoPath}\" -t {duration} {encoderArgs} -c:a aac -b:a 256k -ar 48000 -movflags +faststart \"{clipPath}\"";
        }

        private void CleanupTempFiles(Guid jobId)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            if (Directory.Exists(jobDir))
                Directory.Delete(jobDir, true);
        }

        public async Task DeleteJobAsync(string userId, Guid jobId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var job = await context.ClipJobs
                .Include(j => j.Clips)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

            if (job == null) return;

            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            if (Directory.Exists(jobDir))
                Directory.Delete(jobDir, true);

            context.VideoClips.RemoveRange(job.Clips);
            context.ClipJobs.Remove(job);
            await context.SaveChangesAsync();
        }

        public async Task CleanupPublishedClipsAsync(string userId, Guid jobId)
        {
            _logger.LogInformation("Cleaning up published clips for job {JobId} by user {UserId}", jobId, userId);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var job = await context.ClipJobs
                .Include(j => j.Clips)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found for cleanup", jobId);
                return;
            }

            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            _logger.LogInformation("Job {JobId}: Looking for clips in {Dir}, exists={Exists}", jobId, jobDir, Directory.Exists(jobDir));

            var deletedFiles = 0;
            if (Directory.Exists(jobDir))
            {
                var files = Directory.GetFiles(jobDir);
                _logger.LogInformation("Job {JobId}: Found {Count} files to delete", jobId, files.Length);
                foreach (var file in files)
                {
                    File.Delete(file);
                    deletedFiles++;
                    _logger.LogInformation("Job {JobId}: Deleted file {File}", jobId, Path.GetFileName(file));
                }
                Directory.Delete(jobDir, true);
                _logger.LogInformation("Job {JobId}: Deleted directory {Dir}", jobId, jobDir);
            }
            else
            {
                _logger.LogWarning("Job {JobId}: Directory not found at {Dir}", jobId, jobDir);
            }

            var clipCount = job.Clips.Count;
            context.VideoClips.RemoveRange(job.Clips);
            await context.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {FileCount} files and {DbCount} DB records for job {JobId}", deletedFiles, clipCount, jobId);
        }
    }

    public class ClipSegment
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public double EngagementScore { get; set; }
    }

    public static class ProcessExtensions
    {
        public static async Task WaitForAsync(this Process process, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                throw new TimeoutException("Process timed out");
            }
        }
    }
}

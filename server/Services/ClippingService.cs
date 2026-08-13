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

            _logger.LogInformation("ClippingService initialized: clipsDir={ClipsDir}, yt-dlp={YtDlp}, ffmpeg={Ffmpeg}", _clipsDir, _ytDlpPath ?? "NOT FOUND", _ffmpegPath ?? "NOT FOUND");
        }

        private static string? FindExecutable(string name, string[] knownPaths)
        {
            foreach (var path in knownPaths)
            {
                if (File.Exists(path)) return path;
            }

            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                foreach (var dir in envPath.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(dir.Trim(), name);
                    if (File.Exists(fullPath)) return fullPath;
                }
            }

            return name;
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
                await UpdateProgress(context, job, "downloading", 5, "Starting download...");
                _logger.LogInformation("Job {JobId}: Starting download from {Url}", jobId, job.VideoUrl);

                var (videoPath, audioPath) = await DownloadVideoAndAudio(job.VideoUrl, jobId, job, context);

                await UpdateProgress(context, job, "extracting_audio", 30, "Extracting audio...");
                await ExtractAudio(videoPath, audioPath, job, context);

                await UpdateProgress(context, job, "uploading_audio", 40, "Uploading audio for transcription...");

                var transcriptResult = await TranscribeWithAssemblyAI(audioPath, job, context);

                await UpdateProgress(context, job, "analyzing", 65, "Finding best moments with AI...");

                var highlights = await FindHighlightsWithGPT(transcriptResult.Text, job, context);

                await UpdateProgress(context, job, "generating_clips", 75, $"Generating {highlights.Count} clips...");

                await GenerateClips(videoPath, highlights, job, context);

                await UpdateProgress(context, job, "completed", 100, "All clips ready!");

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
                await UpdateProgress(context, job, "failed", 0, ex.Message);
                await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobFailed", jobId, ex.Message);
                CleanupTempFiles(jobId);
            }
        }

        private async Task UpdateProgress(DbContext_app context, ClipJob job, string status, int progress, string? message = null)
        {
            job.Status = status;
            job.Progress = progress;
            await context.SaveChangesAsync();

            var payload = new { jobId = job.Id, status, progress, message };
            await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobProgress", payload);
            _logger.LogInformation("Job {JobId}: {Status} - {Progress}% - {Message}", job.Id, status, progress, message);
        }

        private async Task<(string videoPath, string audioPath)> DownloadVideoAndAudio(string videoUrl, Guid jobId, ClipJob job, DbContext_app context)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var videoPath = Path.Combine(jobDir, "video.mp4");
            var audioPath = Path.Combine(jobDir, "audio.mp3");

            await UpdateProgress(context, job, "downloading", 5, "Downloading video...");
            _logger.LogInformation("Job {JobId}: Running yt-dlp for URL: {Url}", jobId, videoUrl);

            var ytDlpArgs = $"--no-playlist -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best\" -o \"{videoPath}\" \"{videoUrl}\"";
            var ytDlpResult = await RunProcess(_ytDlpPath, ytDlpArgs);

            if (!File.Exists(videoPath))
            {
                _logger.LogWarning("Job {JobId}: First attempt failed, trying fallback...", jobId);
                var fallbackArgs = $"-f best -o \"{videoPath}\" \"{videoUrl}\"";
                ytDlpResult = await RunProcess(_ytDlpPath, fallbackArgs);
            }

            if (!File.Exists(videoPath))
            {
                throw new Exception($"yt-dlp failed to download video. Error: {ytDlpResult.Error.Substring(0, Math.Min(500, ytDlpResult.Error.Length))}");
            }

            var fileSize = new FileInfo(videoPath).Length;
            _logger.LogInformation("Job {JobId}: Video downloaded successfully. Size: {Size} bytes", jobId, fileSize);
            await UpdateProgress(context, job, "downloaded", 25, $"Video downloaded ({fileSize / 1024 / 1024} MB)");

            return (videoPath, audioPath);
        }

        private async Task ExtractAudio(string videoPath, string audioPath, ClipJob job, DbContext_app context)
        {
            if (!File.Exists(videoPath))
                throw new Exception($"Video file not found: {videoPath}");

            _logger.LogInformation("Job {JobId}: Extracting audio from {VideoPath}", job.Id, videoPath);

            var ffmpegAudioArgs = $"-i \"{videoPath}\" -vn -acodec libmp3lame -ab 128k -ar 44100 \"{audioPath}\"";
            var audioResult = await RunProcess(_ffmpegPath, ffmpegAudioArgs);

            if (!audioResult.Success || !File.Exists(audioPath))
            {
                var errorMsg = audioResult.Error.Length > 500 ? audioResult.Error.Substring(0, 500) : audioResult.Error;
                throw new Exception($"Failed to extract audio: {errorMsg}");
            }

            var audioSize = new FileInfo(audioPath).Length;
            _logger.LogInformation("Job {JobId}: Audio extracted successfully. Size: {Size} bytes", job.Id, audioSize);
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

        private async Task<(bool Success, string Output, string Error)> RunProcessWithProgress(string fileName, string arguments, ClipJob job, DbContext_app context, int startProgress, int endProgress, string label)
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
                    await UpdateProgress(context, job, job.Status, currentProgress, label);
                    await Task.Delay(1000);
                }
            });

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForAsync(TimeSpan.FromMinutes(30));

            await progressTask;

            return (process.ExitCode == 0, await outputTask, await errorTask);
        }

        private async Task<(string Text, string TranscriptId)> TranscribeWithAssemblyAI(string audioPath, ClipJob job, DbContext_app context)
        {
            var client = _httpClientFactory.CreateClient();
            var assemblyKey = _config["AssemblyAI:ApiKey"];
            if (string.IsNullOrEmpty(assemblyKey))
                throw new Exception("AssemblyAI API key not configured");

            client.DefaultRequestHeaders.Add("authorization", assemblyKey);

            var uploadUrl = await UploadAudioToAssemblyAI(client, audioPath);

            var transcriptRequest = new
            {
                audio_url = uploadUrl,
                auto_highlights = true
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

            var completedTranscript = await PollTranscriptStatus(client, transcriptId, job, context);

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

        private async Task<JsonElement> PollTranscriptStatus(HttpClient client, string transcriptId, ClipJob job, DbContext_app context)
        {
            var maxAttempts = 120;
            var delay = TimeSpan.FromSeconds(5);

            for (int i = 0; i < maxAttempts; i++)
            {
                var response = await client.GetAsync($"https://api.assemblyai.com/v2/transcript/{transcriptId}");
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                var status = result.GetProperty("status").GetString();

                var progress = 40 + (int)((i / (double)maxAttempts) * 25);
                await UpdateProgress(context, job, "transcribing", progress, $"Transcribing... ({Math.Round((i / (double)maxAttempts) * 100)}%)");

                if (status == "completed")
                    return result;

                if (status == "error")
                    throw new Exception($"AssemblyAI transcription failed: {result.GetProperty("error").GetString()}");

                await Task.Delay(delay);
            }

            throw new Exception("Transcription timed out");
        }

        private async Task<List<ClipSegment>> FindHighlightsWithGPT(string transcript, ClipJob job, DbContext_app context)
        {
            await UpdateProgress(context, job, "analyzing", 65, "Analyzing transcript with AI...");

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

            await UpdateProgress(context, job, "analyzing", 70, "AI is finding the best moments...");

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

        private async Task GenerateClips(string videoPath, List<ClipSegment> highlights, ClipJob job, DbContext_app context)
        {
            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            Directory.CreateDirectory(jobDir);
            var totalClips = Math.Min(highlights.Count, job.MaxClips);

            if (!File.Exists(videoPath))
                throw new Exception($"Source video not found: {videoPath}");

            var videoSize = new FileInfo(videoPath).Length;
            if (videoSize < 1024)
                throw new Exception($"Source video is too small ({videoSize} bytes). Download likely failed.");

            _logger.LogInformation("Job {JobId}: Source video exists, size={Size} bytes", job.Id, videoSize);

            for (int i = 0; i < totalClips; i++)
            {
                var segment = highlights[i];
                var clipPath = Path.Combine(jobDir, $"clip_{i + 1}.mp4");
                var thumbnailPath = Path.Combine(jobDir, $"thumb_{i + 1}.jpg");

                var duration = segment.EndTime - segment.StartTime;
                var clipProgress = 75 + (int)((i / (double)totalClips) * 10);
                await UpdateProgress(context, job, "generating_clips", clipProgress, $"Generating clip {i + 1}/{totalClips}...");

                var ffmpegArgs = $"-i \"{videoPath}\" -ss {segment.StartTime} -t {duration} -c:v libx264 -preset slow -crf 14 -profile:v high -level 4.1 -pix_fmt yuv420p -c:a aac -b:a 256k -ar 48000 -movflags +faststart \"{clipPath}\"";
                _logger.LogInformation("Job {JobId}: Running FFmpeg for clip {Index}: {Args}", job.Id, i + 1, ffmpegArgs);
                var result = await RunProcess(_ffmpegPath, ffmpegArgs);

                if (!result.Success)
                {
                    _logger.LogWarning("Job {JobId}: FFmpeg failed for clip {Index}: {Error}", job.Id, i + 1, result.Error);
                    continue;
                }

                var clipSize = File.Exists(clipPath) ? new FileInfo(clipPath).Length : 0;
                _logger.LogInformation("Job {JobId}: Clip {Index} generated, size={Size} bytes", job.Id, i + 1, clipSize);

                if (clipSize < 1024)
                {
                    _logger.LogWarning("Job {JobId}: Clip {Index} is too small ({Size} bytes), skipping", job.Id, i + 1, clipSize);
                    continue;
                }

                var thumbArgs = $"-i \"{videoPath}\" -ss {segment.StartTime + (duration / 2)} -vframes 1 -q:v 2 \"{thumbnailPath}\"";
                await RunProcess(_ffmpegPath, thumbArgs);

                var clip = new VideoClip
                {
                    ClipJobId = job.Id,
                    Title = segment.Title,
                    Description = segment.Description,
                    StartTimeSeconds = segment.StartTime,
                    EndTimeSeconds = segment.EndTime,
                    DurationSeconds = duration,
                    S3Key = $"/clips/{job.Id}/clip_{i + 1}.mp4",
                    ThumbnailS3Key = $"/clips/{job.Id}/thumb_{i + 1}.jpg",
                    EngagementScore = segment.EngagementScore
                };

                context.VideoClips.Add(clip);
                await context.SaveChangesAsync();

                _logger.LogInformation("Job {JobId}: Clip {Index} saved to DB", job.Id, i + 1);
            }
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

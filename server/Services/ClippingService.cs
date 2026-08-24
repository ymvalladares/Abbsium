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

            var retentionDays = _config.GetValue<int>("Clipping:ClipRetentionDays");
            if (retentionDays <= 0) retentionDays = 7;
            CleanupOldClipDirs(retentionDays);

            _logger.LogInformation("ClippingService initialized: clipsDir={ClipsDir}, yt-dlp={YtDlp}, ffmpeg={Ffmpeg}, nvenc={Nvenc}, maxParallelClips={MaxParallelClips}", _clipsDir, _ytDlpPath ?? "NOT FOUND", _ffmpegPath ?? "NOT FOUND", _useNvenc, _maxParallelClips);
        }

        private void CleanupOldClipDirs(int retentionDays)
        {
            try
            {
                if (!Directory.Exists(_clipsDir)) return;
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                var removed = 0;
                foreach (var dir in Directory.GetDirectories(_clipsDir))
                {
                    try
                    {
                        if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                        {
                            Directory.Delete(dir, true);
                            removed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not remove old clip dir {Dir}: {Message}", dir, ex.Message);
                    }
                }
                if (removed > 0)
                    _logger.LogInformation("Cleanup: removed {Count} clip directories older than {Days} days", removed, retentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Clip dir cleanup failed: {Message}", ex.Message);
            }
        }

        private static long GetFreeDiskSpace(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady) return drive.AvailableFreeSpace;
                }
            }
            catch { }
            return long.MaxValue;
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
            var testFile = Path.Combine(Path.GetTempPath(), $"abbsium_nvenc_test_{Guid.NewGuid():N}.mp4");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-hide_banner -f lavfi -i testsrc=duration=1:size=320x240:rate=10 -c:v h264_nvenc -preset p1 -pix_fmt yuv420p -y \"{testFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                process.StandardError.ReadToEnd();
                if (!process.WaitForExit(15000)) return false;
                if (process.ExitCode != 0) return false;

                var info = new FileInfo(testFile);
                return info.Exists && info.Length > 1024;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (File.Exists(testFile)) File.Delete(testFile); } catch { }
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

        private async Task<bool> IsH264VideoAsync(string videoPath, CancellationToken cancellationToken = default)
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

        public async Task<ClipJob> CreateJobAsync(string userId, string videoUrl, int maxClips, int minDuration, int maxDuration, string? aspectRatio = null)
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
                AspectRatio = aspectRatio ?? "9:16",
                Status = "pending",
                Progress = 0
            };

            context.ClipJobs.Add(job);
            await context.SaveChangesAsync();

            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessJobAsync(job.Id, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Job {JobId} was cancelled", job.Id);
                }
            }, cts.Token);

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

        public async Task ProcessJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            EnsureJobQueueInitialized(_config);
            await _jobQueue!.WaitAsync(cancellationToken);
            try
            {
                await ProcessJobCoreAsync(jobId, cancellationToken);
            }
            finally
            {
                _jobQueue.Release();
            }
        }

        private async Task ProcessJobCoreAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("=== Starting processing for job {JobId} ===", jobId);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            ClipJob? job;
            try
            {
                job = await context.ClipJobs.FindAsync(new object[] { jobId }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR loading clip job {JobId}: {Message} ===", jobId, ex.Message);
                return;
            }

            if (job == null)
            {
                _logger.LogError("Job {JobId} not found", jobId);
                return;
            }

            _logger.LogInformation("Job {JobId}: Found job, status={Status}", jobId, job.Status);

            try
            {
                await UpdateProgress(job, "downloading", 5, "Downloading audio...", cancellationToken);
                _logger.LogInformation("Job {JobId}: Starting audio download from {Url}", jobId, job.VideoUrl);

                var audioFile = await DownloadAudioOnly(job.VideoUrl, jobId, job, cancellationToken);

                var preparedAudio = await PrepareAudioForTranscription(audioFile, job, cancellationToken);
                if (preparedAudio != audioFile)
                {
                    await UpdateProgress(job, "extracting_audio", 15, "Preparing audio for transcription...", cancellationToken);
                }

                await UpdateProgress(job, "uploading_audio", 20, "Uploading audio for transcription...", cancellationToken);

                var transcriptResult = await TranscribeWithAssemblyAI(preparedAudio, job, cancellationToken);

                await UpdateProgress(job, "analyzing", 60, "Finding best moments with AI...", cancellationToken);

                var highlights = await FindHighlightsWithGPT(transcriptResult.Text, job, cancellationToken);

                await UpdateProgress(job, "generating_clips", 70, $"Downloading {highlights.Count} clip segments...", cancellationToken);

                var (segmentPaths, videoPath) = await DownloadVideoSourceAsync(job.VideoUrl, jobId, job, highlights, cancellationToken);

                await GenerateClips(segmentPaths, videoPath, highlights, job, cancellationToken);

                await UpdateProgress(job, "completed", 100, "All clips ready!", cancellationToken);

                job.Status = "completed";
                job.CompletedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);

                await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobCompleted", job.Id, cancellationToken);
                _logger.LogInformation("=== Job {JobId} completed successfully ===", jobId);

                CleanupTempFiles(jobId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Job {JobId} was cancelled during processing", jobId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR processing clip job {JobId}: {Message} ===", jobId, ex.Message);
                await UpdateProgress(job, "failed", 0, ex.Message, cancellationToken);
                await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobFailed", jobId, ex.Message, cancellationToken);
                CleanupTempFiles(jobId);
            }
        }

        private async Task UpdateProgress(ClipJob job, string status, int progress, string? message = null, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var current = await context.ClipJobs.FindAsync(new object[] { job.Id }, cancellationToken);
            if (current == null) return;

            current.Status = status;
            current.Progress = progress;
            await context.SaveChangesAsync(cancellationToken);

            var payload = new { jobId = job.Id, status, progress, message };
            await _hubContext.Clients.User(job.UserId).SendAsync("ClipJobProgress", payload, cancellationToken);
            _logger.LogInformation("Job {JobId}: {Status} - {Progress}% - {Message}", job.Id, status, progress, message);
        }

        private async Task<string> GetYtDlpVersionAsync()
        {
            try
            {
                var result = await RunProcess(_ytDlpPath, "--version", default);
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                    return result.Output.Trim();
            }
            catch { }
            return string.Empty;
        }

        private async Task<string> DownloadAudioOnly(string videoUrl, Guid jobId, ClipJob job, CancellationToken cancellationToken = default)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            var maxRetries = 6;
            Exception? lastException = null;

            var clientConfigs = new[]
            {
                new { Label = "web", Args = "", Format = "bestaudio[ext=m4a]/bestaudio/best" },
                new { Label = "web_safari", Args = "--extractor-args \"youtube:player_client=web_safari\"", Format = "bestaudio/best" },
                new { Label = "android", Args = "--extractor-args \"youtube:player_client=android\"", Format = "bestaudio/best" },
                new { Label = "ios", Args = "--extractor-args \"youtube:player_client=ios\"", Format = "bestaudio/best" },
                new { Label = "tv", Args = "--extractor-args \"youtube:player_client=tv\"", Format = "bestaudio/best" },
                new { Label = "web_any", Args = "", Format = "best" }
            };

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var configIndex = Math.Min(attempt - 1, clientConfigs.Length - 1);
                var config = clientConfigs[configIndex];

                _logger.LogInformation("Job {JobId}: yt-dlp audio attempt {Attempt}/{Max} (config: {Config}, format: {Format})", jobId, attempt, maxRetries, config.Label, config.Format);

                var outputTemplate = Path.Combine(jobDir, "audio.%(ext)s");
                var ytDlpArgs = $"--no-playlist -f \"{config.Format}\" " +
                    $"--geo-bypass --force-ipv4 --no-check-certificates " +
                    $"--user-agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\" " +
                    $"{cookiesArg}" +
                    $"{config.Args} " +
                    $"-o \"{outputTemplate}\" \"{videoUrl}\"";

                var ytDlpResult = await RunProcess(_ytDlpPath, ytDlpArgs, cancellationToken);

                var audioPath = FindDownloadedFile(jobDir, "audio");
                if (audioPath != null && new FileInfo(audioPath).Length > 1024)
                {
                    var fileSize = new FileInfo(audioPath).Length;
                    _logger.LogInformation("Job {JobId}: Audio downloaded on attempt {Attempt} ({Config}). Size: {Size} bytes", jobId, attempt, config.Label, fileSize);
                    return audioPath;
                }

                lastException = new Exception($"yt-dlp ({config.Label}) failed: {ytDlpResult.Error.Substring(0, Math.Min(500, ytDlpResult.Error.Length))}");
                _logger.LogWarning("Job {JobId}: Audio attempt {Attempt} failed ({Config}, {Format}): {Error}", jobId, attempt, config.Label, config.Format, lastException.Message);

                if (attempt < maxRetries)
                {
                    var delayMs = (int)Math.Pow(3, attempt) * 1000;
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            var isFatal = lastException != null &&
                (lastException.Message.Contains("403") ||
                 lastException.Message.Contains("format is not available") ||
                 lastException.Message.Contains("Only images"));

            if (isFatal)
            {
                _logger.LogWarning("Job {JobId}: All yt-dlp audio attempts failed. Trying full video download as fallback...", jobId);
                try
                {
                    var videoPath = await DownloadFullVideo(videoUrl, jobId, job, cancellationToken);
                    _logger.LogInformation("Job {JobId}: Fallback video downloaded, will extract audio during transcription prep", jobId);
                    return videoPath;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Job {JobId}: Fallback video download also failed", jobId);
                }
            }

            throw new Exception($"yt-dlp failed to download audio after {maxRetries} attempts. Tried configs: {string.Join(", ", clientConfigs.Select(c => c.Label))}. Last error: {lastException?.Message}");
        }

        private static string? FindDownloadedFile(string dir, string namePrefix)
        {
            return Directory.GetFiles(dir, $"{namePrefix}.*")
                .FirstOrDefault(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                                  && !f.EndsWith(".ytdl", StringComparison.OrdinalIgnoreCase)
                                  && new FileInfo(f).Length > 0);
        }

        private async Task<string> PrepareAudioForTranscription(string audioPath, ClipJob job, CancellationToken cancellationToken = default)
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
            var audioResult = await RunProcess(_ffmpegPath, ffmpegAudioArgs, cancellationToken);

            if (!audioResult.Success || !File.Exists(mp3Path))
            {
                var errorMsg = audioResult.Error.Length > 500 ? audioResult.Error.Substring(0, 500) : audioResult.Error;
                throw new Exception($"Failed to prepare audio for transcription: {errorMsg}");
            }

            var audioSize = new FileInfo(mp3Path).Length;
            _logger.LogInformation("Job {JobId}: Audio converted to MP3. Size: {Size} bytes", job.Id, audioSize);
            return mp3Path;
        }

        private async Task<List<string?>> DownloadVideoSegments(string videoUrl, Guid jobId, ClipJob job, List<ClipSegment> highlights, CancellationToken cancellationToken = default)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            var baseFlags = "--geo-bypass --force-ipv4 --no-check-certificates " +
                $"--user-agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\" " +
                $"--add-header \"Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8\" " +
                $"--add-header \"Accept-Language: en-US,en;q=0.9\" " +
                $"--add-header \"Sec-Fetch-Dest: document\" " +
                $"--add-header \"Sec-Fetch-Mode: navigate\" " +
                $"--add-header \"Sec-Fetch-Site: none\" " +
                $"--add-header \"Sec-Fetch-User: ?1\" " +
                $"--add-header \"Sec-Ch-Ua: \\\"Chromium\\\";v=\\\"131\\\", \\\"Not_A Brand\\\";v=\\\"24\\\"\" " +
                $"--add-header \"Sec-Ch-Ua-Mobile: ?0\" " +
                $"--add-header \"Sec-Ch-Ua-Platform: \\\"Windows\\\"\" ";

            var clientConfigs = new[]
            {
                "--extractor-args \"youtube:player_client=android\" --extractor-args \"youtube:player_skip=webpage\"",
                "--extractor-args \"youtube:player_client=ios\"",
                "--extractor-args \"youtube:player_client=tv\"",
                "--extractor-args \"youtube:player_client=web\""
            };

            var segmentPaths = new string?[highlights.Count];
            var semaphore = new SemaphoreSlim(3, 3);
            var tasks = new List<Task>();

            for (int i = 0; i < highlights.Count; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var segment = highlights[index];
                        var segmentPath = Path.Combine(jobDir, $"segment_{index + 1}.mp4");
                        _logger.LogInformation("Job {JobId}: Downloading segment {Index} ({Start}s-{End}s)", jobId, index + 1, segment.StartTime, segment.EndTime);

                        var maxRetries = 4;
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            var configIndex = Math.Min(attempt - 1, clientConfigs.Length - 1);
                            var config = clientConfigs[configIndex];

                            var args = $"--no-playlist --download-sections \"*{segment.StartTime}-{segment.EndTime}\" -f \"bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/bestvideo[ext=mp4]+bestaudio/best\" -S res:1080 {baseFlags}{cookiesArg}{config} -o \"{segmentPath}\" \"{videoUrl}\"";

                            var result = await RunProcess(_ytDlpPath, args, cancellationToken);

                            if (File.Exists(segmentPath) && new FileInfo(segmentPath).Length > 1024)
                            {
                                segmentPaths[index] = segmentPath;
                                _logger.LogInformation("Job {JobId}: Segment {Index} downloaded on attempt {Attempt}, size={Size} bytes", jobId, index + 1, attempt, new FileInfo(segmentPath).Length);
                                break;
                            }

                            if (attempt < maxRetries)
                            {
                                var delayMs = (int)Math.Pow(3, attempt) * 1000;
                                _logger.LogWarning("Job {JobId}: Segment {Index} failed on attempt {Attempt} (config {ConfigIdx}), retrying in {Delay}ms...", jobId, index + 1, attempt, configIndex + 1, delayMs);
                                await Task.Delay(delayMs, cancellationToken);
                            }
                            else
                            {
                                var err = result.Error.Length > 300 ? result.Error.Substring(0, 300) : result.Error;
                                _logger.LogWarning("Job {JobId}: Segment {Index} download failed after {Attempts} attempts: {Error}", jobId, index + 1, maxRetries, err);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            return segmentPaths.ToList();
        }

        private async Task<string> DownloadFullVideo(string videoUrl, Guid jobId, ClipJob job, CancellationToken cancellationToken = default)
        {
            var jobDir = Path.Combine(_tempDir, jobId.ToString());
            Directory.CreateDirectory(jobDir);

            var videoPath = Path.Combine(jobDir, "video.mp4");

            var cookiesFile = _config["Youtube:CookiesFile"];
            var cookiesArg = !string.IsNullOrEmpty(cookiesFile) && File.Exists(cookiesFile) ? $"--cookies \"{cookiesFile}\" " : "";

            var ytDlpVersion = await GetYtDlpVersionAsync();
            if (!string.IsNullOrEmpty(ytDlpVersion))
                _logger.LogInformation("Job {JobId}: yt-dlp version: {Version}", jobId, ytDlpVersion);

            var maxRetries = 6;
            Exception? lastException = null;

            var clientConfigs = new[]
            {
                new { Label = "web", Args = "" },
                new { Label = "web_safari", Args = "--extractor-args \"youtube:player_client=web_safari\"" },
                new { Label = "android", Args = "--extractor-args \"youtube:player_client=android\"" },
                new { Label = "ios", Args = "--extractor-args \"youtube:player_client=ios\"" },
                new { Label = "tv", Args = "--extractor-args \"youtube:player_client=tv\"" },
                new { Label = "tv_embedded", Args = "--extractor-args \"youtube:player_client=tv_embedded\"" }
            };

            var formatSelectors = new[]
            {
                "bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/bestvideo[ext=mp4]+bestaudio/best",
                "bestvideo+bestaudio/best",
                "best"
            };

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                var configIndex = Math.Min(attempt - 1, clientConfigs.Length - 1);
                var config = clientConfigs[configIndex];
                var fmtIndex = Math.Min(attempt / 2, formatSelectors.Length - 1);
                var fmt = formatSelectors[fmtIndex];

                _logger.LogInformation("Job {JobId}: yt-dlp video attempt {Attempt}/{Max} (config: {Config}, format: {Format})", jobId, attempt, maxRetries, config.Label, fmt);

                var ytDlpArgs = $"--no-playlist -f \"{fmt}\" -S res:1080 " +
                    $"--geo-bypass --force-ipv4 --no-check-certificates " +
                    $"--user-agent \"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\" " +
                    $"{cookiesArg}" +
                    $"{config.Args} " +
                    $"-o \"{videoPath}\" \"{videoUrl}\"";

                var ytDlpResult = await RunProcess(_ytDlpPath, ytDlpArgs, cancellationToken);

                if (File.Exists(videoPath) && new FileInfo(videoPath).Length > 1024)
                {
                    var fileSize = new FileInfo(videoPath).Length;
                    _logger.LogInformation("Job {JobId}: Video downloaded on attempt {Attempt} ({Config}). Size: {Size} bytes", jobId, attempt, config.Label, fileSize);
                    return videoPath;
                }

                lastException = new Exception($"yt-dlp ({config.Label}) failed: {ytDlpResult.Error.Substring(0, Math.Min(500, ytDlpResult.Error.Length))}");
                _logger.LogWarning("Job {JobId}: Video attempt {Attempt} failed ({Config}, {Format}): {Error}", jobId, attempt, config.Label, fmt, lastException.Message);

                if (attempt < maxRetries)
                {
                    var delayMs = (int)Math.Pow(3, attempt) * 1000;
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            throw new Exception($"yt-dlp failed to download video after {maxRetries} attempts (yt-dlp version: {ytDlpVersion ?? "unknown"}). Tried configs: {string.Join(", ", clientConfigs.Select(c => c.Label))}. Last error: {lastException?.Message}");
        }

        private async Task<(List<string>? SegmentPaths, string? VideoPath)> DownloadVideoSourceAsync(string videoUrl, Guid jobId, ClipJob job, List<ClipSegment> highlights, CancellationToken cancellationToken = default)
        {
            var clipCount = Math.Min(highlights.Count, job.MaxClips);
            var segments = await DownloadVideoSegments(videoUrl, jobId, job, highlights.Take(clipCount).ToList(), cancellationToken);

            if (segments.All(s => !string.IsNullOrEmpty(s)))
            {
                _logger.LogInformation("Job {JobId}: All {Count} clip segments downloaded successfully", jobId, segments.Count);
                return (segments.Select(s => s!).ToList(), null);
            }

            var okCount = segments.Count(s => !string.IsNullOrEmpty(s));
            _logger.LogWarning("Job {JobId}: Segment download incomplete ({OkCount}/{Total}), falling back to full video download...", jobId, okCount, segments.Count);
            var videoPath = await DownloadFullVideo(videoUrl, jobId, job, cancellationToken);
            return (null, videoPath);
        }

        private async Task<(bool Success, string Output, string Error)> RunProcess(string fileName, string arguments, CancellationToken cancellationToken = default)
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

            var tcs = new TaskCompletionSource<bool>();
            using var reg = cancellationToken.Register(() =>
            {
                try { process.Kill(); } catch { }
                tcs.TrySetCanceled(cancellationToken);
            });

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) => tcs.TrySetResult(true);
            if (process.HasExited) tcs.TrySetResult(true);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(10)));
            if (completed != tcs.Task)
            {
                try { process.Kill(); } catch { }
                throw new TimeoutException("Process timed out");
            }

            return (process.ExitCode == 0, await outputTask, await errorTask);
        }

        private async Task<(string Text, string TranscriptId)> TranscribeWithAssemblyAI(string audioPath, ClipJob job, CancellationToken cancellationToken = default)
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

            var response = await client.PostAsync("https://api.assemblyai.com/v2/transcript", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var transcriptId = result.GetProperty("id").GetString() ?? throw new Exception("No transcript ID");

            var completedTranscript = await PollTranscriptStatus(client, transcriptId, job, cancellationToken);

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

        private async Task<JsonElement> PollTranscriptStatus(HttpClient client, string transcriptId, ClipJob job, CancellationToken cancellationToken = default)
        {
            var maxAttempts = 120;
            var delay = TimeSpan.FromSeconds(2);

            for (int i = 0; i < maxAttempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var response = await client.GetAsync($"https://api.assemblyai.com/v2/transcript/{transcriptId}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var status = result.GetProperty("status").GetString();

                var progress = 25 + (int)((i / (double)maxAttempts) * 30);
                await UpdateProgress(job, "transcribing", progress, $"Transcribing... ({Math.Round((i / (double)maxAttempts) * 100)}%)", cancellationToken);

                if (status == "completed")
                    return result;

                if (status == "error")
                    throw new Exception($"AssemblyAI transcription failed: {result.GetProperty("error").GetString()}");

                await Task.Delay(delay, cancellationToken);
            }

            throw new Exception("Transcription timed out");
        }

        private async Task<List<ClipSegment>> FindHighlightsWithGPT(string transcript, ClipJob job, CancellationToken cancellationToken = default)
        {
            await UpdateProgress(job, "analyzing", 60, "Analyzing transcript with AI...", cancellationToken);

            var apiKey = _config["OpenAI:ApiKey"];
            var client = new ChatClient("gpt-4o-mini", apiKey);

            var systemPrompt = @"You are an expert video content analyst and viral content strategist. Your job is to find the most engaging, viral-worthy segments from a video transcript.

Return ONLY a JSON array of clip segments. Each segment must have:
- ""title"": A catchy, click-worthy title for the clip (max 60 chars)
- ""description"": Brief description of what happens in the clip
- ""start_time"": Start time in seconds (MUST be different for each clip)
- ""end_time"": End time in seconds (MUST be at least 30 seconds after start_time)
- ""engagement_score"": Score from 0-100 based on how viral-worthy it is
- ""hashtags"": Array of 4-5 relevant hashtags (include #fyp and #viral)
- ""hook_text"": A compelling first-line hook to grab attention (max 40 chars)
- ""best_time_to_post"": Best time to post this clip for maximum engagement (e.g. ""9:00 AM"", ""12:30 PM"", ""6:00 PM"")

CRITICAL RULES:
- Each clip MUST cover a DIFFERENT part of the video. Do NOT return the same timestamps.
- Clips should be 30-60 seconds each.
- Spread clips across the entire video duration. First clip at the beginning, last clip near the end.
- NEVER return start_time=0 for all clips. Distribute them: e.g., 0-45, 120-165, 300-345.
- Return maximum 5 clips.
- Hashtags should be relevant to the content, not generic. Always include #fyp and #viral.
- Hook text should be a first-person or curiosity-driven statement.
- Best time to post should vary across clips (morning, lunch, evening, night).
- Return ONLY the JSON array, no other text";

            var userMessage = $"Here is the transcript:\n\n{transcript}\n\nFind the most engaging clips from this transcript.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            await UpdateProgress(job, "analyzing", 65, "AI is finding the best moments...", cancellationToken);

            var response = await client.CompleteChatAsync(messages, cancellationToken: cancellationToken);
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

                    if (seg.Hashtags == null || seg.Hashtags.Count == 0)
                    {
                        seg.Hashtags = new List<string> { "#viral", "#fyp", "#trending", "#foryou" };
                    }

                    if (string.IsNullOrEmpty(seg.HookText))
                    {
                        seg.HookText = "Wait for it...";
                    }

                    if (string.IsNullOrEmpty(seg.BestTimeToPost))
                    {
                        var times = new[] { "9:00 AM", "12:30 PM", "6:00 PM", "8:30 PM" };
                        seg.BestTimeToPost = times[segments.IndexOf(seg) % times.Length];
                    }
                }
            }

            return segments;
        }

        private async Task GenerateClips(List<string>? segmentPaths, string? videoPath, List<ClipSegment> highlights, ClipJob job, CancellationToken cancellationToken = default)
        {
            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            Directory.CreateDirectory(jobDir);
            var totalClips = Math.Min(highlights.Count, job.MaxClips);

            if (totalClips <= 0)
            {
                _logger.LogWarning("Job {JobId}: No clips to generate", job.Id);
                return;
            }

            var freeSpace = GetFreeDiskSpace(jobDir);
            if (freeSpace < 2L * 1024 * 1024 * 1024)
            {
                var freeMb = freeSpace / (1024 * 1024);
                throw new Exception($"Insufficient disk space ({freeMb} MB free). Delete old clips or free up disk space.");
            }

            if (videoPath != null && !File.Exists(videoPath))
                throw new Exception($"Source video not found: {videoPath}");

            _logger.LogInformation("Job {JobId}: Generating {Total} clips from {Source} (aspect ratio: {AspectRatio})", job.Id, totalClips, videoPath ?? "segments", job.AspectRatio);

            var maxParallel = Math.Max(1, Math.Min(_maxParallelClips, totalClips));
            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            var tasks = new List<Task>();
            var successCount = 0;

            for (int i = 0; i < totalClips; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (await GenerateSingleClipAsync(segmentPaths?[index], videoPath, highlights[index], job, index, totalClips, jobDir, cancellationToken))
                            Interlocked.Increment(ref successCount);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks);

            if (successCount == 0)
                throw new Exception("All clips failed to generate. See server logs for ffmpeg errors.");

            _logger.LogInformation("Job {JobId}: Uploading {Count} clips to S3...", job.Id, successCount);
            await UploadClipsToS3Async(job, jobDir, cancellationToken);
        }

        private async Task<bool> GenerateSingleClipAsync(string? segmentPath, string? videoPath, ClipSegment segment, ClipJob job, int index, int totalClips, string jobDir, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var clipPath = Path.Combine(jobDir, $"clip_{index + 1}.mp4");
            var thumbnailPath = Path.Combine(jobDir, $"thumb_{index + 1}.jpg");

            var duration = segment.EndTime - segment.StartTime;
            var clipProgress = 80 + (int)((index / (double)totalClips) * 20);
            await UpdateProgress(job, "generating_clips", clipProgress, $"Generating clip {index + 1}/{totalClips}...", cancellationToken);

            var sourcePath = segmentPath ?? videoPath!;
            var useStreamCopy = await IsH264VideoAsync(sourcePath, cancellationToken);
            var useNvenc = _useNvenc;
            var aspectRatio = job.AspectRatio ?? "9:16";

            (bool Success, string Output, string Error) result = (false, string.Empty, string.Empty);

            if (segmentPath != null)
            {
                if (useStreamCopy && aspectRatio == "16:9")
                {
                    var remuxArgs = $"-i \"{segmentPath}\" -c copy -movflags +faststart -y \"{clipPath}\"";
                    _logger.LogInformation("Job {JobId}: Remuxing segment for clip {Index}: {Args}", job.Id, index + 1, remuxArgs);
                    result = await RunProcess(_ffmpegPath, remuxArgs, cancellationToken);

                    if (!result.Success)
                    {
                        var err = result.Error.Length > 300 ? result.Error.Substring(0, 300) : result.Error;
                        _logger.LogWarning("Job {JobId}: Remux failed for clip {Index}, re-encoding: {Error}", job.Id, index + 1, err);
                        result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath, useNvenc, aspectRatio), cancellationToken);

                        if (!result.Success && useNvenc)
                        {
                            _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                            useNvenc = false;
                            result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath, useNvenc, aspectRatio), cancellationToken);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Job {JobId}: Re-encoding segment {Index} with aspect ratio {AR}...", job.Id, index + 1, aspectRatio);
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath, useNvenc, aspectRatio), cancellationToken);

                    if (!result.Success && useNvenc)
                    {
                        _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                        useNvenc = false;
                        result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(0, duration, segmentPath, clipPath, useNvenc, aspectRatio), cancellationToken);
                    }
                }
            }
            else if (useStreamCopy && aspectRatio == "16:9")
            {
                var copyArgs = $"-ss {segment.StartTime} -i \"{videoPath}\" -t {duration} -c:v copy -c:a copy -movflags +faststart -y \"{clipPath}\"";
                _logger.LogInformation("Job {JobId}: Running FFmpeg (stream copy) for clip {Index}: {Args}", job.Id, index + 1, copyArgs);
                result = await RunProcess(_ffmpegPath, copyArgs, cancellationToken);

                if (!result.Success)
                {
                    _logger.LogWarning("Job {JobId}: Stream copy failed for clip {Index}, retrying with re-encode: {Error}", job.Id, index + 1, result.Error.Substring(0, Math.Min(300, result.Error.Length)));
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath, useNvenc, aspectRatio), cancellationToken);

                    if (!result.Success && useNvenc)
                    {
                        _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                        useNvenc = false;
                        result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath, useNvenc, aspectRatio), cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogInformation("Job {JobId}: Re-encoding clip {Index} with aspect ratio {AR}...", job.Id, index + 1, aspectRatio);
                result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath, useNvenc, aspectRatio), cancellationToken);

                if (!result.Success && useNvenc)
                {
                    _logger.LogWarning("Job {JobId}: NVENC failed for clip {Index}, retrying with libx264...", job.Id, index + 1);
                    useNvenc = false;
                    result = await RunProcess(_ffmpegPath, BuildClipFfmpegArgs(segment.StartTime, duration, videoPath!, clipPath, useNvenc, aspectRatio), cancellationToken);
                }
            }

            var clipSize = File.Exists(clipPath) ? new FileInfo(clipPath).Length : 0;
            _logger.LogInformation("Job {JobId}: Clip {Index} generated, size={Size} bytes", job.Id, index + 1, clipSize);

            if (clipSize < 1024)
            {
                var err = result.Error.Length > 300 ? result.Error.Substring(0, 300) : result.Error;
                _logger.LogWarning("Job {JobId}: Clip {Index} is too small ({Size} bytes), skipping. FFmpeg error: {Error}", job.Id, index + 1, clipSize, err);
                return false;
            }

            var thumbArgs = $"-ss {duration / 2} -i \"{clipPath}\" -vframes 1 -q:v 2 -y \"{thumbnailPath}\"";
            await RunProcess(_ffmpegPath, thumbArgs, cancellationToken);

            var hashtagsJson = segment.Hashtags != null ? string.Join(",", segment.Hashtags) : null;

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
                EngagementScore = segment.EngagementScore,
                Hashtags = hashtagsJson,
                HookText = segment.HookText,
                BestTimeToPost = segment.BestTimeToPost
            };

            context.VideoClips.Add(clip);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Job {JobId}: Clip {Index} saved to DB", job.Id, index + 1);
            return true;
        }

        private string BuildClipFfmpegArgs(int startTime, int duration, string videoPath, string clipPath, bool useNvenc, string? aspectRatio = null)
        {
            var encoderArgs = useNvenc
                ? "-c:v h264_nvenc -preset p4 -rc vbr -cq 19 -b:v 0 -profile:v high -level 4.1 -pix_fmt yuv420p"
                : "-c:v libx264 -preset veryfast -crf 16 -profile:v high -level 4.1 -pix_fmt yuv420p";

            var cropFilter = BuildCropFilter(aspectRatio ?? "9:16");
            var filterArg = string.IsNullOrEmpty(cropFilter) ? "" : $"-vf \"{cropFilter}\" ";

            return $"-ss {startTime} -i \"{videoPath}\" -t {duration} {filterArg}{encoderArgs} -c:a aac -b:a 256k -ar 48000 -movflags +faststart \"{clipPath}\"";
        }

        private string BuildCropFilter(string aspectRatio)
        {
            return aspectRatio switch
            {
                "9:16" => "crop=ih*(9/16):ih:(iw-ih*(9/16))/2:0",
                "1:1" => "crop=min(iw\\,ih):min(iw\\,ih):(iw-min(iw\\,ih))/2:(ih-min(iw\\,ih))/2",
                "16:9" => "",
                _ => ""
            };
        }

        private async Task UploadClipsToS3Async(ClipJob job, string jobDir, CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var clips = await context.VideoClips
                .Where(c => c.ClipJobId == job.Id)
                .ToListAsync(cancellationToken);

            foreach (var clip in clips)
            {
                if (string.IsNullOrEmpty(clip.S3Key)) continue;

                var clipFileName = Path.GetFileName(clip.S3Key.TrimStart('/'));
                var clipFilePath = Path.Combine(jobDir, clipFileName);

                if (File.Exists(clipFilePath))
                {
                    var success = await _s3Service.UploadFileAsync(clip.S3Key, clipFilePath, "video/mp4", cancellationToken);
                    _logger.LogInformation("Job {JobId}: Clip {ClipId} upload to S3: {Success}", job.Id, clip.Id, success ? "OK" : "FAILED");
                }

                if (!string.IsNullOrEmpty(clip.ThumbnailS3Key))
                {
                    var thumbFileName = Path.GetFileName(clip.ThumbnailS3Key.TrimStart('/'));
                    var thumbFilePath = Path.Combine(jobDir, thumbFileName);

                    if (File.Exists(thumbFilePath))
                    {
                        var success = await _s3Service.UploadFileAsync(clip.ThumbnailS3Key, thumbFilePath, "image/jpeg", cancellationToken);
                        _logger.LogInformation("Job {JobId}: Thumbnail {ClipId} upload to S3: {Success}", job.Id, clip.Id, success ? "OK" : "FAILED");
                    }
                }
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

            var s3Prefix = $"/clips/{job.Id}";
            var deletedS3 = await _s3Service.DeleteObjectsByPrefixAsync(s3Prefix.TrimStart('/'));
            _logger.LogInformation("Job {JobId}: Deleted {Count} objects from S3", jobId, deletedS3);

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

            var s3Prefix = $"/clips/{job.Id}";
            var deletedS3 = await _s3Service.DeleteObjectsByPrefixAsync(s3Prefix.TrimStart('/'));
            _logger.LogInformation("Job {JobId}: Deleted {Count} objects from S3", jobId, deletedS3);

            var jobDir = Path.Combine(_clipsDir, job.Id.ToString());
            _logger.LogInformation("Job {JobId}: Looking for clips in {Dir}, exists={Exists}", jobId, jobDir, Directory.Exists(jobDir));

            var deletedFiles = 0;
            if (Directory.Exists(jobDir))
            {
                var files = Directory.GetFiles(jobDir);
                _logger.LogInformation("Job {JobId}: Found {Count} files to delete locally", jobId, files.Length);
                foreach (var file in files)
                {
                    File.Delete(file);
                    deletedFiles++;
                    _logger.LogInformation("Job {JobId}: Deleted local file {File}", jobId, Path.GetFileName(file));
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

            _logger.LogInformation("Cleaned up {S3Count} S3 objects, {FileCount} local files and {DbCount} DB records for job {JobId}", deletedS3, deletedFiles, clipCount, jobId);
        }
    }

    public class ClipSegment
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public double EngagementScore { get; set; }
        public List<string>? Hashtags { get; set; }
        public string? HookText { get; set; }
        public string? BestTimeToPost { get; set; }
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

using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services;
using Server.Services.SocialMedia;
using Server.Services.SocialMedia.Interfaces;

namespace Server.Services
{
    public class ScheduledPostWorker : BackgroundService
    {
        private readonly ILogger<ScheduledPostWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ScheduledPostWorker(ILogger<ScheduledPostWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScheduledPostWorker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDuePostsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ScheduledPostWorker processing cycle");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            _logger.LogInformation("ScheduledPostWorker stopped");
        }

        private async Task ProcessDuePostsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext_app>();

            var now = DateTime.UtcNow;
            var duePostIds = await db.ScheduledPosts
                .Where(x => x.Status == "scheduled" && x.ScheduledFor <= now)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var postId in duePostIds)
            {
                // Atomic claim
                var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ScheduledPosts SET Status = 'processing' WHERE Id = {postId} AND Status = 'scheduled'",
                    cancellationToken);

                if (rows == 0) continue;

                await ProcessScheduledPostAsync(postId, scope.ServiceProvider, cancellationToken);
            }
        }

        private async Task ProcessScheduledPostAsync(Guid postId, IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext_app>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ISocialMediaOrchestrator>();
            var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ScheduledPostWorker>>();

            var post = await db.ScheduledPosts.FindAsync(new object[] { postId }, cancellationToken);
            if (post == null || post.Status != "processing") return;

            try
            {
                var platforms = System.Text.Json.JsonSerializer.Deserialize<List<SocialPlatform>>(post.Platforms);
                if (platforms == null || platforms.Count == 0)
                {
                    post.Status = "failed";
                    post.ErrorMessage = "No valid platforms found";
                    post.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                }

                var downloadUrl = s3Service.GeneratePresignedDownloadUrl(post.S3Key);
                var isVideo = !string.IsNullOrEmpty(post.S3Url) && (post.S3Url.EndsWith(".mp4") || post.S3Url.EndsWith(".mov") || post.S3Url.EndsWith(".avi"));

                var request = new SocialPostRequest
                {
                    Platforms = platforms,
                    VideoUrl = isVideo ? downloadUrl : null,
                    PhotoUrl = isVideo ? null : downloadUrl,
                    Title = post.Title,
                    Caption = post.Caption,
                    IsShort = post.IsShort,
                    PageId = post.PageId,
                    YouTubePlaylistId = post.YouTubePlaylistId,
                    ThumbnailUrl = post.ThumbnailUrl
                };

                var session = new PublishSession
                {
                    UserId = post.UserId,
                    S3Key = post.S3Key,
                    S3Url = post.S3Url,
                    Caption = post.Title ?? post.Caption ?? "",
                    Platforms = post.Platforms,
                    Status = "processing"
                };

                db.PublishSessions.Add(session);
                await db.SaveChangesAsync(cancellationToken);

                var sessionId = session.Id.ToString();
                post.PublishSessionId = sessionId;
                await db.SaveChangesAsync(cancellationToken);

                MultiPlatformPostResult multiResult;

                if (platforms.Count == 1)
                {
                    var result = await orchestrator.PublishToSinglePlatformAsync(post.UserId, platforms[0], request, sessionId);
                    await SaveHistoryAsync(db, logger, post.UserId, result);
                    multiResult = new MultiPlatformPostResult { Results = new List<SocialPostResult> { result } };
                }
                else
                {
                    multiResult = await orchestrator.PublishToPlatformsAsync(post.UserId, request, sessionId);
                    foreach (var result in multiResult.Results)
                    {
                        await SaveHistoryAsync(db, logger, post.UserId, result);
                    }
                }

                post.Status = multiResult.AllSuccessful ? "completed" : "failed";
                post.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE PublishSessions SET Status = {(multiResult.AllSuccessful ? "completed" : "failed")}, CompletedAt = {DateTime.UtcNow} WHERE Id = {Guid.Parse(sessionId)}",
                    cancellationToken);

                logger.LogInformation("Scheduled post {PostId} processed. Status: {Status}", postId, post.Status);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process scheduled post {PostId}", postId);
                post.Status = "failed";
                post.ErrorMessage = ex.Message;
                post.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task SaveHistoryAsync(DbContext_app db, ILogger logger, string userId, SocialPostResult result)
        {
            try
            {
                db.PostHistory.Add(new PostHistory
                {
                    UserId = userId,
                    Platform = result.Platform.ToString(),
                    Success = result.Success,
                    PostId = result.PostId,
                    PostUrl = result.PostUrl,
                    ErrorMessage = result.ErrorMessage,
                    PublishedAt = result.PublishedAt
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save post history for user {UserId}", userId);
            }
        }
    }
}

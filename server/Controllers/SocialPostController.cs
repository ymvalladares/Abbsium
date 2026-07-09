using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services;
using Server.Services.SocialMedia;
using Server.Services.SocialMedia.Interfaces;
using System.Security.Claims;

namespace Server.Controllers
{
    [Authorize]
    public class SocialPostController : Base_Control_Api
    {
        private readonly ISocialMediaOrchestrator _orchestrator;
        private readonly DbContext_app _db;
        private readonly ILogger<SocialPostController> _logger;
        private readonly IS3Service _s3Service;
        private readonly IServiceScopeFactory _scopeFactory;

        public SocialPostController(
            ISocialMediaOrchestrator orchestrator,
            DbContext_app db,
            ILogger<SocialPostController> logger,
            IS3Service s3Service,
            IServiceScopeFactory scopeFactory)
        {
            _orchestrator = orchestrator;
            _db = db;
            _logger = logger;
            _s3Service = s3Service;
            _scopeFactory = scopeFactory;
        }

        [HttpPost("s3/presigned")]
        public async Task<IActionResult> GetPresignedUrl([FromBody] PresignedUrlRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var result = await _s3Service.GeneratePresignedUploadUrlAsync(request.FileName, request.ContentType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating presigned URL for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to generate upload URL" });
            }
        }

        [HttpPost("publish")]
        public async Task<IActionResult> Publish([FromBody] SocialPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (request.Platforms == null || request.Platforms.Count == 0)
                return BadRequest("At least one platform is required");

            _logger.LogInformation("Publish request from user {UserId} to platforms: {Platforms}",
                userId, string.Join(", ", request.Platforms));

            try
            {
                var s3Url = request.VideoUrl ?? request.PhotoUrl ?? "";
                var s3Key = ExtractKeyFromUrl(s3Url);
                
                var session = new PublishSession
                {
                    UserId = userId,
                    S3Key = s3Key,
                    S3Url = s3Url,
                    Caption = request.Title ?? request.Caption ?? "",
                    Platforms = System.Text.Json.JsonSerializer.Serialize(request.Platforms),
                    Status = "processing"
                };
                
                _db.PublishSessions.Add(session);
                await _db.SaveChangesAsync();

                var sessionId = session.Id.ToString();
                var platformsJson = session.Platforms;
                var downloadUrl = _s3Service.GeneratePresignedDownloadUrl(s3Key);
                var title = request.Title;
                var caption = request.Caption;
                var isShort = request.IsShort;
                var pageId = request.PageId;
                var youTubePlaylistId = request.YouTubePlaylistId;
                var thumbnailUrl = request.ThumbnailUrl;
                var isVideo = !string.IsNullOrEmpty(request.VideoUrl);

                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DbContext_app>();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<ISocialMediaOrchestrator>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<SocialPostController>>();

                    var bgRequest = new SocialPostRequest
                    {
                        Platforms = System.Text.Json.JsonSerializer.Deserialize<List<SocialPlatform>>(platformsJson) ?? new List<SocialPlatform>(),
                        VideoUrl = isVideo ? downloadUrl : null,
                        PhotoUrl = isVideo ? null : downloadUrl,
                        Title = title,
                        Caption = caption,
                        IsShort = isShort,
                        PageId = pageId,
                        YouTubePlaylistId = youTubePlaylistId,
                        ThumbnailUrl = thumbnailUrl
                    };

                    try
                    {
                        MultiPlatformPostResult multiResult;
                        
                        if (bgRequest.Platforms.Count == 1)
                        {
                            var result = await orchestrator.PublishToSinglePlatformAsync(
                                userId, bgRequest.Platforms[0], bgRequest, sessionId);
                            await SaveHistoryAsync(db, logger, userId, result);
                            multiResult = new MultiPlatformPostResult { Results = new List<SocialPostResult> { result } };
                        }
                        else
                        {
                            multiResult = await orchestrator.PublishToPlatformsAsync(userId, bgRequest, sessionId);
                            foreach (var result in multiResult.Results)
                            {
                                await SaveHistoryAsync(db, logger, userId, result);
                            }
                        }
                        
                        var sessionIdGuid = Guid.Parse(sessionId);
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE PublishSessions SET Status = {0}, CompletedAt = {1} WHERE Id = {2}",
                            multiResult.AllSuccessful ? "completed" : "failed",
                            DateTime.UtcNow,
                            sessionIdGuid);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background publishing failed for session {SessionId}", sessionId);
                        
                        var sessionIdGuid = Guid.Parse(sessionId);
                        await db.Database.ExecuteSqlRawAsync(
                            "UPDATE PublishSessions SET Status = 'failed', CompletedAt = {0} WHERE Id = {1}",
                            DateTime.UtcNow,
                            sessionIdGuid);

                        try
                        {
                            var platforms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(platformsJson) ?? new List<string>();
                            foreach (var platform in platforms)
                            {
                                await orchestrator.SendErrorEventAsync(userId, platform, sessionId, ex.Message);
                            }
                        }
                        catch { }
                    }
                });

                return Ok(new { sessionId, message = "Publishing started", status = "processing" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing post for user {UserId}", userId);
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var total = await _db.PostHistory.CountAsync(x => x.UserId == userId);

            var history = await _db.PostHistory
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    id = x.Id,
                    platform = x.Platform,
                    success = x.Success,
                    postId = x.PostId,
                    postUrl = x.PostUrl,
                    errorMessage = x.ErrorMessage,
                    publishedAt = x.PublishedAt
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize),
                items = history
            });
        }

        [HttpPost("history/cleanup")]
        public async Task<IActionResult> CleanupOldHistory([FromQuery] int daysToKeep = 30)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldRecords = await _db.PostHistory
                .Where(x => x.UserId == userId && x.PublishedAt < cutoff)
                .ToListAsync();

            _db.PostHistory.RemoveRange(oldRecords);
            var deleted = await _db.SaveChangesAsync();

            _logger.LogInformation("Cleaned up {Deleted} old post history records for user {UserId}", deleted, userId);
            return Ok(new { message = $"Deleted {deleted} records older than {daysToKeep} days" });
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

        private object MapToFrontendResponse(SocialPostResult result)
        {
            var response = new
            {
                success = result.Success,
                platform = result.Platform.ToString(),
                postId = result.PostId,
                postUrl = result.PostUrl,
                errorMessage = result.ErrorMessage,
                publishedAt = result.PublishedAt
            };

            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                var parts = result.ErrorMessage.Split(": ", 2);
                return new
                {
                    response.success,
                    response.platform,
                    response.postId,
                    response.postUrl,
                    response.publishedAt,
                    errorTitle = parts.Length > 0 ? parts[0] : result.ErrorMessage,
                    errorAction = parts.Length > 1 ? parts[1] : null
                };
            }

            return response;
        }

        private object MapToFrontendMultiResponse(MultiPlatformPostResult result)
        {
            var mappedResults = new List<object>();

            foreach (var r in result.Results)
            {
                if (!r.Success && !string.IsNullOrEmpty(r.ErrorMessage))
                {
                    var parts = r.ErrorMessage.Split(": ", 2);
                    mappedResults.Add(new
                    {
                        success = r.Success,
                        platform = r.Platform.ToString(),
                        postId = r.PostId,
                        postUrl = r.PostUrl,
                        publishedAt = r.PublishedAt,
                        errorTitle = parts.Length > 0 ? parts[0] : r.ErrorMessage,
                        errorAction = parts.Length > 1 ? parts[1] : null
                    });
                }
                else
                {
                    mappedResults.Add(new
                    {
                        success = r.Success,
                        platform = r.Platform.ToString(),
                        postId = r.PostId,
                        postUrl = r.PostUrl,
                        errorMessage = r.ErrorMessage,
                        publishedAt = r.PublishedAt
                    });
                }
            }

            return new
            {
                totalPlatforms = result.TotalPlatforms,
                successfulPosts = result.SuccessfulPosts,
                failedPosts = result.FailedPosts,
                allSuccessful = result.AllSuccessful,
                results = mappedResults
            };
        }
        
        private string ExtractKeyFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            var uri = new Uri(url);
            return uri.LocalPath.TrimStart('/');
        }
    }
    
    public class PresignedUrlRequest
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
    }
}

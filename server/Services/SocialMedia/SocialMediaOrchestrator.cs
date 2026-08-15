using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server.Chat.HubFolder;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services.SocialMedia.Interfaces;

namespace Server.Services.SocialMedia
{
    public interface ISocialMediaOrchestrator
    {
        Task<MultiPlatformPostResult> PublishToPlatformsAsync(string userId, SocialPostRequest request, string? sessionId = null);
        Task<SocialPostResult> PublishToSinglePlatformAsync(string userId, SocialPlatform platform, SocialPostRequest request, string? sessionId = null);
        Task SendErrorEventAsync(string userId, string platform, string sessionId, string errorMessage);
    }

    public class SocialMediaOrchestrator : ISocialMediaOrchestrator
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SocialMediaOrchestrator> _logger;
        private readonly IHubContext<PublishingHub> _hubContext;

        public SocialMediaOrchestrator(
            IServiceScopeFactory scopeFactory,
            ILogger<SocialMediaOrchestrator> logger,
            IHubContext<PublishingHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task<MultiPlatformPostResult> PublishToPlatformsAsync(string userId, SocialPostRequest request, string? sessionId = null)
        {
            var result = new MultiPlatformPostResult();

            await _hubContext.Clients.Group($"publish_{userId}").SendAsync("publish_started", new
            {
                sessionId,
                totalPlatforms = request.Platforms.Count
            });

            var tasks = request.Platforms.Select(platform =>
                PublishToSinglePlatformAsync(userId, platform, request, sessionId));

            var results = await Task.WhenAll(tasks);
            result.Results.AddRange(results);

            await _hubContext.Clients.Group($"publish_{userId}").SendAsync("publish_finished", new
            {
                sessionId,
                successful = result.SuccessfulPosts,
                failed = result.FailedPosts,
                total = result.TotalPlatforms
            });

            _logger.LogInformation(
                "Multi-platform post completed for user {UserId}: {Success}/{Total} successful",
                userId, result.SuccessfulPosts, result.TotalPlatforms);

            return result;
        }

        public async Task<SocialPostResult> PublishToSinglePlatformAsync(string userId, SocialPlatform platform, SocialPostRequest request, string? sessionId = null)
        {
            using var scope = _scopeFactory.CreateScope();

            var publisher = scope.ServiceProvider.GetServices<ISocialPublisher>()
                .FirstOrDefault(p => p.Platform == platform);

            if (publisher == null)
            {
                return new SocialPostResult
                {
                    Platform = platform,
                    Success = false,
                    ErrorMessage = $"Publisher for {platform} not found"
                };
            }

            await _hubContext.Clients.Group($"publish_{userId}").SendAsync("network_status", new
            {
                sessionId,
                network = platform.ToString(),
                status = "uploading",
                message = $"Publicando en {platform}..."
            });

            SocialPostResult result;

            if (!string.IsNullOrEmpty(request.VideoUrl))
            {
                result = await publisher.PublishVideoAsync(userId, request);
            }
            else if (!string.IsNullOrEmpty(request.PhotoUrl))
            {
                result = await publisher.PublishPhotoAsync(userId, request);
            }
            else
            {
                result = await publisher.PublishTextAsync(userId, request);
            }

            if (result.Success)
            {
                await _hubContext.Clients.Group($"publish_{userId}").SendAsync("network_status", new
                {
                    sessionId,
                    network = platform.ToString(),
                    status = "success",
                    message = $"{platform}: Publicado ✅",
                    postId = result.PostId,
                    postUrl = result.PostUrl
                });
            }
            else
            {
                await _hubContext.Clients.Group($"publish_{userId}").SendAsync("network_status", new
                {
                    sessionId,
                    network = platform.ToString(),
                    status = "error",
                    message = $"{platform}: Error ❌",
                    error = result.ErrorMessage
                });
            }

            return result;
        }

        public async Task SendErrorEventAsync(string userId, string platform, string sessionId, string errorMessage)
        {
            await _hubContext.Clients.Group($"publish_{userId}").SendAsync("network_status", new
            {
                sessionId,
                network = platform,
                status = "error",
                message = $"{platform}: Error ❌",
                error = errorMessage
            });

            await _hubContext.Clients.Group($"publish_{userId}").SendAsync("publish_finished", new
            {
                sessionId,
                successful = 0,
                failed = 1,
                total = 1
            });
        }
    }
}

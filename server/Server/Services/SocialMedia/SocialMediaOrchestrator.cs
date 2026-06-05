using Microsoft.Extensions.Logging;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services.SocialMedia.Interfaces;

namespace Server.Services.SocialMedia
{
    public interface ISocialMediaOrchestrator
    {
        Task<MultiPlatformPostResult> PublishToPlatformsAsync(string userId, SocialPostRequest request);
        Task<SocialPostResult> PublishToSinglePlatformAsync(string userId, SocialPlatform platform, SocialPostRequest request);
    }

    public class SocialMediaOrchestrator : ISocialMediaOrchestrator
    {
        private readonly IEnumerable<ISocialPublisher> _publishers;
        private readonly ILogger<SocialMediaOrchestrator> _logger;

        public SocialMediaOrchestrator(
            IEnumerable<ISocialPublisher> publishers,
            ILogger<SocialMediaOrchestrator> logger)
        {
            _publishers = publishers;
            _logger = logger;
        }

        public async Task<MultiPlatformPostResult> PublishToPlatformsAsync(string userId, SocialPostRequest request)
        {
            var result = new MultiPlatformPostResult();

            var tasks = request.Platforms.Select(async platform =>
            {
                return await PublishToSinglePlatformAsync(userId, platform, request);
            });

            var results = await Task.WhenAll(tasks);
            result.Results.AddRange(results);

            _logger.LogInformation(
                "Multi-platform post completed for user {UserId}: {Success}/{Total} successful",
                userId, result.SuccessfulPosts, result.TotalPlatforms);

            return result;
        }

        public async Task<SocialPostResult> PublishToSinglePlatformAsync(string userId, SocialPlatform platform, SocialPostRequest request)
        {
            var publisher = _publishers.FirstOrDefault(p => p.Platform == platform);
            if (publisher == null)
            {
                return new SocialPostResult
                {
                    Platform = platform,
                    Success = false,
                    ErrorMessage = $"Publisher for {platform} not found"
                };
            }

            if (!string.IsNullOrEmpty(request.VideoUrl))
            {
                return await publisher.PublishVideoAsync(userId, request);
            }

            if (!string.IsNullOrEmpty(request.PhotoUrl))
            {
                return await publisher.PublishPhotoAsync(userId, request);
            }

            return await publisher.PublishTextAsync(userId, request);
        }
    }
}

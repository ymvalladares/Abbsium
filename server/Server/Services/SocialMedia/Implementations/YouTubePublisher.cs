using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services.SocialMedia.Base;
using Server.Services.SocialMedia.Interfaces;
using System.Text.Json;

namespace Server.Services.SocialMedia.Implementations
{
    public class YouTubePublisher : SocialServiceBase, ISocialPublisher
    {
        public SocialPlatform Platform => SocialPlatform.YouTube;
        protected override string ProviderName => "youtube";

        public YouTubePublisher(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<YouTubePublisher> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public async Task<SocialPostResult> PublishTextAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.YouTube };
            result.ErrorMessage = "YouTube does not support text-only posts. Use video instead.";
            return result;
        }

        public async Task<SocialPostResult> PublishPhotoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.YouTube };
            result.ErrorMessage = "YouTube does not support photo-only posts. Use video instead.";
            return result;
        }

        public async Task<SocialPostResult> PublishVideoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.YouTube };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "YouTube not connected";
                    return result;
                }

                if (string.IsNullOrEmpty(request.VideoUrl))
                {
                    result.ErrorMessage = "VideoUrl is required";
                    return result;
                }

                var videoBytes = request.VideoUrl.StartsWith("data:")
                    ? ExtractBase64Media(request.VideoUrl)
                    : await DownloadMediaAsync(request.VideoUrl);

                var videoId = await UploadVideoToYouTube(
                    acc.AccessToken,
                    videoBytes,
                    request.Caption ?? request.Message ?? "",
                    request.Message ?? "",
                    request.IsShort);

                if (string.IsNullOrEmpty(videoId))
                {
                    result.ErrorMessage = "Failed to upload video to YouTube";
                    return result;
                }

                result.Success = true;
                result.PostId = videoId;
                result.PostUrl = $"https://www.youtube.com/watch?v={videoId}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to YouTube for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<string?> UploadVideoToYouTube(string accessToken, byte[] videoBytes, string title, string description, bool isShort)
        {
            var client = _httpClientFactory.CreateClient();

            var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            var content = new MultipartFormDataContent(boundary);

            var metadata = new
            {
                snippet = new
                {
                    title = title,
                    description = description,
                    tags = isShort ? new[] { "#Shorts" } : Array.Empty<string>(),
                    categoryId = "22"
                },
                status = new
                {
                    privacyStatus = "public"
                }
            };

            var metadataJson = JsonSerializer.Serialize(metadata);
            var metadataContent = new StringContent(metadataJson, System.Text.Encoding.UTF8, "application/json");
            content.Add(metadataContent, "metadata");

            content.Add(new ByteArrayContent(videoBytes), "video", "video.mp4");

            var url = "https://www.googleapis.com/upload/youtube/v3/videos?part=snippet,status";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("YouTube upload failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idElem))
            {
                return idElem.GetString();
            }

            return null;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;
using Server.Services.SocialMedia.Base;
using Server.Services.SocialMedia.Interfaces;
using Server.Services;
using System.Text.Json;

namespace Server.Services.SocialMedia.Implementations
{
    public class TikTokPublisher : SocialServiceBase, ISocialPublisher
    {
        public SocialPlatform Platform => SocialPlatform.TikTok;
        protected override string ProviderName => "tiktok";

        public TikTokPublisher(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<TikTokPublisher> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public async Task<SocialPostResult> PublishTextAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.TikTok };
            result.ErrorMessage = "TikTok does not support text-only posts. Use video instead.";
            return result;
        }

        public async Task<SocialPostResult> PublishPhotoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.TikTok };
            result.ErrorMessage = "TikTok does not support photo-only posts. Use video instead.";
            return result;
        }

        public async Task<SocialPostResult> PublishVideoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.TikTok };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "TikTok not connected";
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

                _logger.LogInformation("Uploading video to TikTok: {Size} bytes", videoBytes.Length);

                var publishId = await UploadAndPublishVideo(
                    acc.AccessToken,
                    videoBytes,
                    request.Caption ?? request.Title ?? "");

                if (string.IsNullOrEmpty(publishId))
                {
                    result.ErrorMessage = "Error uploading the video to TikTok. Please ensure it is MP4 with a duration between 3 and 10 minutes.";
                    return result;
                }

                result.Success = true;
                result.PostId = publishId;
                result.PostUrl = $"https://www.tiktok.com/@user/video/{publishId}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to TikTok for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<string?> UploadAndPublishVideo(string accessToken, byte[] videoBytes, string description)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");

            var sessionId = await InitiateUpload(accessToken, videoBytes.Length, description);
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("TikTok upload initiation failed");
                return null;
            }

            var uploadSuccess = await UploadVideoChunks(accessToken, sessionId, videoBytes);
            if (!uploadSuccess)
            {
                _logger.LogWarning("TikTok video upload failed");
                return null;
            }

            var publishId = await PublishVideo(accessToken, sessionId, description);
            return publishId;
        }

        private async Task<string?> InitiateUpload(string accessToken, long videoSize, string description)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");
            var url = "https://open.tiktokapis.com/v2/post/publish/content/init/";

            var body = new
            {
                post_info = new
                {
                    title = description,
                    privacy_level = "PUBLIC",
                    disable_comment = false,
                    disable_duet = false,
                    disable_stitch = false,
                    video_cover_timestamp_ms = 1000
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = videoSize
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok upload init failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("publish_id", out var pubIdElem))
            {
                return pubIdElem.GetString();
            }

            return null;
        }

        private async Task<bool> UploadVideoChunks(string accessToken, string publishId, byte[] videoBytes)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");
            var url = $"https://open.tiktokapis.com/v2/post/publish/content/upload/?publish_id={publishId}";

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new ByteArrayContent(videoBytes)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");

            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private async Task<string?> PublishVideo(string accessToken, string publishId, string description)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");
            var url = "https://open.tiktokapis.com/v2/post/publish/content/fetch/";

            var body = new { publish_id = publishId };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok publish failed: {Response}", responseString);
                return null;
            }

            return publishId;
        }
    }
}

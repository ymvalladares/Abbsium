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
    public class InstagramPublisher : SocialServiceBase, ISocialPublisher
    {
        public SocialPlatform Platform => SocialPlatform.Instagram;
        protected override string ProviderName => "instagram";

        public InstagramPublisher(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<InstagramPublisher> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public async Task<SocialPostResult> PublishTextAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Instagram };
            result.ErrorMessage = "Instagram does not support text-only posts. Use photo or video instead.";
            return result;
        }

        public async Task<SocialPostResult> PublishPhotoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Instagram };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "Instagram not connected";
                    return result;
                }

                if (string.IsNullOrEmpty(request.PhotoUrl))
                {
                    result.ErrorMessage = "PhotoUrl is required";
                    return result;
                }

                var igUserId = await GetInstagramBusinessAccountId(acc.AccessToken);
                if (string.IsNullOrEmpty(igUserId))
                {
                    result.ErrorMessage = "Instagram Business account not found. Connect a Business account.";
                    return result;
                }

                var imageUrl = request.PhotoUrl;
                if (imageUrl.StartsWith("data:"))
                {
                    imageUrl = await UploadBase64ImageAsTempUrl(request.PhotoUrl);
                }

                var containerId = await CreateMediaContainer(igUserId, acc.AccessToken, imageUrl, request.Caption ?? request.Message ?? "");
                if (string.IsNullOrEmpty(containerId))
                {
                    result.ErrorMessage = "Failed to create media container";
                    return result;
                }

                var publishedMediaId = await PublishMediaContainer(igUserId, acc.AccessToken, containerId);
                if (string.IsNullOrEmpty(publishedMediaId))
                {
                    result.ErrorMessage = "Failed to publish media";
                    return result;
                }

                result.Success = true;
                result.PostId = publishedMediaId;
                result.PostUrl = $"https://www.instagram.com/p/{publishedMediaId}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting photo to Instagram for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        public async Task<SocialPostResult> PublishVideoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Instagram };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "Instagram not connected";
                    return result;
                }

                if (string.IsNullOrEmpty(request.VideoUrl))
                {
                    result.ErrorMessage = "VideoUrl is required";
                    return result;
                }

                var igUserId = await GetInstagramBusinessAccountId(acc.AccessToken);
                if (string.IsNullOrEmpty(igUserId))
                {
                    result.ErrorMessage = "Instagram Business account not found. Connect a Business account.";
                    return result;
                }

                var videoUrl = request.VideoUrl;
                if (videoUrl.StartsWith("data:"))
                {
                    videoUrl = await UploadBase64VideoAsTempUrl(request.VideoUrl);
                }

                var mediaType = request.IsShort ? "REELS" : "VIDEO";
                var containerId = await CreateVideoContainer(igUserId, acc.AccessToken, videoUrl, request.Caption ?? request.Message ?? "", mediaType);
                if (string.IsNullOrEmpty(containerId))
                {
                    result.ErrorMessage = "Failed to create video container";
                    return result;
                }

                var publishedMediaId = await PublishMediaContainer(igUserId, acc.AccessToken, containerId);
                if (string.IsNullOrEmpty(publishedMediaId))
                {
                    result.ErrorMessage = "Failed to publish video";
                    return result;
                }

                result.Success = true;
                result.PostId = publishedMediaId;
                result.PostUrl = $"https://www.instagram.com/{(request.IsShort ? "reel" : "p")}/{publishedMediaId}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to Instagram for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<string?> GetInstagramBusinessAccountId(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/me?fields=instagram_business_account&access_token={accessToken}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("instagram_business_account", out var igAccount) &&
                igAccount.TryGetProperty("id", out var idElem))
            {
                return idElem.GetString();
            }

            return null;
        }

        private async Task<string?> CreateMediaContainer(string igUserId, string accessToken, string imageUrl, string caption)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/{igUserId}/media";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["image_url"] = imageUrl,
                ["caption"] = caption,
                ["access_token"] = accessToken
            });

            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram container creation failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
        }

        private async Task<string?> CreateVideoContainer(string igUserId, string accessToken, string videoUrl, string caption, string mediaType)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/{igUserId}/media";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["video_url"] = videoUrl,
                ["caption"] = caption,
                ["media_type"] = mediaType,
                ["access_token"] = accessToken
            });

            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram video container creation failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
        }

        private async Task<string?> PublishMediaContainer(string igUserId, string accessToken, string containerId)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/{igUserId}/media_publish";

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["creation_id"] = containerId,
                ["access_token"] = accessToken
            });

            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Instagram media publish failed: {Response}", responseString);
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
        }

        private async Task<string> UploadBase64ImageAsTempUrl(string base64Data)
        {
            var bytes = ExtractBase64Media(base64Data);
            var fileName = $"ig_{Guid.NewGuid()}.jpg";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            await File.WriteAllBytesAsync(tempPath, bytes);

            var wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "temp");
            Directory.CreateDirectory(wwwroot);
            var publicPath = Path.Combine(wwwroot, fileName);
            File.Copy(tempPath, publicPath, true);
            File.Delete(tempPath);

            var appUrl = _config["AppUrl"] ?? "https://localhost:44328";
            return $"{appUrl}/temp/{fileName}";
        }

        private async Task<string> UploadBase64VideoAsTempUrl(string base64Data)
        {
            var bytes = ExtractBase64Media(base64Data);
            var fileName = $"ig_{Guid.NewGuid()}.mp4";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            await File.WriteAllBytesAsync(tempPath, bytes);

            var wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "temp");
            Directory.CreateDirectory(wwwroot);
            var publicPath = Path.Combine(wwwroot, fileName);
            File.Copy(tempPath, publicPath, true);
            File.Delete(tempPath);

            var appUrl = _config["AppUrl"] ?? "https://localhost:44328";
            return $"{appUrl}/temp/{fileName}";
        }
    }
}

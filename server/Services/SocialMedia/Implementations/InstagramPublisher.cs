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

                var caption = BuildInstagramCaption(request);
                var (containerId, containerError) = await CreateMediaContainer(igUserId, acc.AccessToken, imageUrl, caption);
                if (string.IsNullOrEmpty(containerId))
                {
                    result.ErrorMessage = containerError ?? "Error creating the post. Please verify the image is valid and try again.";
                    return result;
                }

                var (isReady, statusError) = await WaitForContainerReady(igUserId, acc.AccessToken, containerId);
                if (!isReady)
                {
                    result.ErrorMessage = statusError ?? "Image processing timed out. Please try again.";
                    return result;
                }

                var (publishedMediaId, publishError) = await PublishMediaContainer(igUserId, acc.AccessToken, containerId);
                if (string.IsNullOrEmpty(publishedMediaId))
                {
                    result.ErrorMessage = publishError ?? "Error publishing to Instagram. Please try again in a few minutes.";
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

                var mediaType = "REELS";
                var caption = BuildInstagramCaption(request);
                var (containerId, containerError) = await CreateVideoContainer(igUserId, acc.AccessToken, videoUrl, caption, mediaType);
                if (string.IsNullOrEmpty(containerId))
                {
                    result.ErrorMessage = containerError ?? "Error creating the video. Please ensure it is MP4 and under 90 seconds for posts, or 15 minutes for Reels.";
                    return result;
                }

                var (isReady, statusError) = await WaitForContainerReady(igUserId, acc.AccessToken, containerId);
                if (!isReady)
                {
                    result.ErrorMessage = statusError ?? "Video processing timed out. Please try again.";
                    return result;
                }

                var (publishedMediaId, publishError) = await PublishMediaContainer(igUserId, acc.AccessToken, containerId);
                if (string.IsNullOrEmpty(publishedMediaId))
                {
                    result.ErrorMessage = publishError ?? "Error publishing the video to Instagram. Please try again in a few minutes.";
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

            // Step 1: Get the page ID from the token
            var meUrl = $"https://graph.facebook.com/v19.0/me?fields=id&access_token={accessToken}";
            var meResponse = await client.GetAsync(meUrl);
            if (!meResponse.IsSuccessStatusCode)
            {
                var errorContent = await meResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to get page ID from token: {Response}", errorContent);
                return null;
            }

            var meJson = await meResponse.Content.ReadAsStringAsync();
            using var meDoc = JsonDocument.Parse(meJson);
            var meRoot = meDoc.RootElement;

            if (!meRoot.TryGetProperty("id", out var pageIdElem))
            {
                _logger.LogWarning("Page ID not found in token response");
                return null;
            }

            var pageId = pageIdElem.GetString();

            // Step 2: Get Instagram Business Account from the page
            var pageUrl = $"https://graph.facebook.com/v19.0/{pageId}?fields=instagram_business_account&access_token={accessToken}";
            var pageResponse = await client.GetAsync(pageUrl);
            if (!pageResponse.IsSuccessStatusCode)
            {
                var errorContent = await pageResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to get Instagram Business Account from page: {Response}", errorContent);
                return null;
            }

            var pageJson = await pageResponse.Content.ReadAsStringAsync();
            using var pageDoc = JsonDocument.Parse(pageJson);
            var pageRoot = pageDoc.RootElement;

            if (pageRoot.TryGetProperty("instagram_business_account", out var igAccount) &&
                igAccount.TryGetProperty("id", out var idElem))
            {
                var igId = idElem.GetString();
                _logger.LogInformation("Found Instagram Business Account: {IgAccountId} for page {PageId}", igId, pageId);
                return igId;
            }

            _logger.LogWarning("No Instagram Business Account found on page {PageId}", pageId);
            return null;
        }

        private async Task<(string? Id, string? Error)> CreateMediaContainer(string igUserId, string accessToken, string imageUrl, string caption)
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
                var errorMessage = ExtractInstagramErrorMessage(responseString);
                return (null, errorMessage);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return (root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null, null);
        }

        private async Task<(string? Id, string? Error)> CreateVideoContainer(string igUserId, string accessToken, string videoUrl, string caption, string mediaType)
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
                var errorMessage = ExtractInstagramErrorMessage(responseString);
                return (null, errorMessage);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return (root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null, null);
        }

        private async Task<(string? Id, string? Error)> PublishMediaContainer(string igUserId, string accessToken, string containerId)
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
                var errorMessage = ExtractInstagramErrorMessage(responseString);
                return (null, errorMessage);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return (root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null, null);
        }

        private string? ExtractInstagramErrorMessage(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString();
                }
            }
            catch { }

            return responseString;
        }

        private async Task<(bool IsReady, string? Error)> WaitForContainerReady(string igUserId, string accessToken, string containerId)
        {
            var client = _httpClientFactory.CreateClient();
            var maxAttempts = 30;
            var delayMs = 5000;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(delayMs);
                }

                var url = $"https://graph.facebook.com/v19.0/{containerId}?fields=status_code&access_token={accessToken}";
                var response = await client.GetAsync(url);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Instagram container status check failed: {Response}", responseString);
                    continue;
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("status_code", out var statusCodeElem))
                {
                    var statusCode = statusCodeElem.GetString();

                    if (statusCode == "FINISHED")
                    {
                        _logger.LogInformation("Instagram container {ContainerId} is ready", containerId);
                        return (true, null);
                    }

                    if (statusCode == "ERROR")
                    {
                        var errorMsg = root.TryGetProperty("error_message", out var msgElem)
                            ? msgElem.GetString()
                            : "Container processing failed";
                        _logger.LogWarning("Instagram container {ContainerId} error: {Error}", containerId, errorMsg);
                        return (false, errorMsg);
                    }

                    if (statusCode == "PUBLISHED")
                    {
                        _logger.LogInformation("Instagram container {ContainerId} already published", containerId);
                        return (true, null);
                    }

                    _logger.LogInformation("Instagram container {ContainerId} status: {Status} (attempt {Attempt}/{Max})", containerId, statusCode, attempt + 1, maxAttempts);
                }
            }

            _logger.LogWarning("Instagram container {ContainerId} timed out after {MaxAttempts} attempts", containerId, maxAttempts);
            return (false, "Media processing timed out. Please try again later.");
        }

        private string BuildInstagramCaption(SocialPostRequest request)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                parts.Add(request.Title.Trim());
            }

            if (!string.IsNullOrWhiteSpace(request.Caption))
            {
                parts.Add(request.Caption.Trim());
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n\n", parts);
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

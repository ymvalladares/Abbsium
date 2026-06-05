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
    public class FacebookPublisher : SocialServiceBase, ISocialPublisher
    {
        public SocialPlatform Platform => SocialPlatform.Facebook;
        protected override string ProviderName => "facebook";

        public FacebookPublisher(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<FacebookPublisher> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public async Task<SocialPostResult> PublishTextAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Facebook };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "Facebook not connected";
                    return result;
                }

                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;
                var fbUrl = string.IsNullOrEmpty(pageId)
                    ? "https://graph.facebook.com/me/feed"
                    : $"https://graph.facebook.com/{pageId}/feed";

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["message"] = request.Message ?? "",
                    ["access_token"] = acc.AccessToken
                });

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Facebook API error: {responseString}";
                    return result;
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                var postId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;

                result.Success = true;
                result.PostId = postId;
                result.PostUrl = postId != null ? $"https://www.facebook.com/{postId}" : null;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting text to Facebook for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        public async Task<SocialPostResult> PublishPhotoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Facebook };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "Facebook not connected";
                    return result;
                }

                if (string.IsNullOrEmpty(request.PhotoUrl))
                {
                    result.ErrorMessage = "PhotoUrl is required";
                    return result;
                }

                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;
                var fbUrl = string.IsNullOrEmpty(pageId)
                    ? "https://graph.facebook.com/me/photos"
                    : $"https://graph.facebook.com/{pageId}/photos";

                var content = new MultipartFormDataContent();
                byte[] bytes;

                if (request.PhotoUrl.StartsWith("data:"))
                {
                    bytes = ExtractBase64Media(request.PhotoUrl);
                }
                else if (request.PhotoUrl.StartsWith("http"))
                {
                    bytes = await DownloadMediaAsync(request.PhotoUrl);
                }
                else
                {
                    result.ErrorMessage = "Invalid PhotoUrl format";
                    return result;
                }

                content.Add(new ByteArrayContent(bytes), "source", "photo.jpg");
                content.Add(new StringContent(request.Message ?? ""), "message");
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Facebook API error: {responseString}";
                    return result;
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                var postId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;

                result.Success = true;
                result.PostId = postId;
                result.PostUrl = postId != null ? $"https://www.facebook.com/{postId}" : null;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting photo to Facebook for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        public async Task<SocialPostResult> PublishVideoAsync(string userId, SocialPostRequest request)
        {
            var result = new SocialPostResult { Platform = SocialPlatform.Facebook };

            try
            {
                var acc = await GetSocialAccountAsync(userId);
                if (acc == null)
                {
                    result.ErrorMessage = "Facebook not connected";
                    return result;
                }

                if (string.IsNullOrEmpty(request.VideoUrl))
                {
                    result.ErrorMessage = "VideoUrl is required";
                    return result;
                }

                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;
                var fbUrl = string.IsNullOrEmpty(pageId)
                    ? "https://graph.facebook.com/me/videos"
                    : $"https://graph.facebook.com/{pageId}/videos";

                var content = new MultipartFormDataContent();
                byte[] bytes;

                if (request.VideoUrl.StartsWith("data:"))
                {
                    bytes = ExtractBase64Media(request.VideoUrl);
                }
                else if (request.VideoUrl.StartsWith("http"))
                {
                    bytes = await DownloadMediaAsync(request.VideoUrl);
                }
                else
                {
                    result.ErrorMessage = "Invalid VideoUrl format";
                    return result;
                }

                content.Add(new ByteArrayContent(bytes), "source", "video.mp4");
                content.Add(new StringContent(request.Message ?? ""), "description");
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    result.ErrorMessage = $"Facebook API error: {responseString}";
                    return result;
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                var postId = root.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;

                result.Success = true;
                result.PostId = postId;
                result.PostUrl = postId != null ? $"https://www.facebook.com/{postId}" : null;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to Facebook for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
    }
}

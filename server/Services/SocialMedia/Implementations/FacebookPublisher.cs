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

                var client = _httpClientFactory.CreateClient("SocialMedia");
                var pageId = request.PageId ?? acc.ProviderAccountId;

                if (string.IsNullOrEmpty(pageId))
                {
                    result.ErrorMessage = "Facebook Page ID is required. Connect a Facebook Page first.";
                    return result;
                }

                var fbUrl = $"https://graph.facebook.com/{pageId}/feed";

                var message = !string.IsNullOrEmpty(request.Title)
                    ? (string.IsNullOrEmpty(request.Caption) ? request.Title : $"{request.Title}\n\n{request.Caption}")
                    : (request.Caption ?? "");

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["message"] = message,
                    ["access_token"] = acc.AccessToken
                });

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    var friendlyTitle = SocialErrorMapper.MapFacebookError(responseString, out var userAction);
                    result.ErrorMessage = $"{friendlyTitle}: {userAction}";
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

                var client = _httpClientFactory.CreateClient("SocialMedia");
                var pageId = request.PageId ?? acc.ProviderAccountId;

                if (string.IsNullOrEmpty(pageId))
                {
                    result.ErrorMessage = "Facebook Page ID is required. Connect a Facebook Page first.";
                    return result;
                }

                var fbUrl = $"https://graph.facebook.com/{pageId}/photos";

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

                var photoMessage = !string.IsNullOrEmpty(request.Title)
                    ? (string.IsNullOrEmpty(request.Caption) ? request.Title : $"{request.Title}\n\n{request.Caption}")
                    : (request.Caption ?? "");

                content.Add(new ByteArrayContent(bytes), "source", "photo.jpg");
                content.Add(new StringContent(photoMessage), "message");
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                if (!fbResponse.IsSuccessStatusCode)
                {
                    var friendlyTitle = SocialErrorMapper.MapFacebookError(responseString, out var userAction);
                    result.ErrorMessage = $"{friendlyTitle}: {userAction}";
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

                var client = _httpClientFactory.CreateClient("SocialMedia");
                var pageId = request.PageId ?? acc.ProviderAccountId;

                if (string.IsNullOrEmpty(pageId))
                {
                    result.ErrorMessage = "Facebook Page ID is required. Connect a Facebook Page first.";
                    return result;
                }

                var fbUrl = $"https://graph.facebook.com/{pageId}/videos";

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

                _logger.LogInformation("Uploading video to Facebook: {Size} bytes ({SizeMB} MB) to {Url}", 
                    bytes.Length, (bytes.Length / 1024.0 / 1024.0).ToString("F2"), fbUrl);

                if (bytes.Length > 256 * 1024 * 1024)
                {
                    result.ErrorMessage = "Video too large for Facebook (max 256MB)";
                    return result;
                }

                var videoMessage = !string.IsNullOrEmpty(request.Title)
                    ? (string.IsNullOrEmpty(request.Caption) ? request.Title : $"{request.Title}\n\n{request.Caption}")
                    : (request.Caption ?? "");

                content.Add(new ByteArrayContent(bytes), "source", "video.mp4");
                if (!string.IsNullOrEmpty(request.Title))
                {
                    content.Add(new StringContent(request.Title), "title");
                }
                content.Add(new StringContent(videoMessage), "description");
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Facebook video upload response: {StatusCode} - {Response}", 
                    fbResponse.StatusCode, responseString.Length > 500 ? responseString.Substring(0, 500) : responseString);

                if (!fbResponse.IsSuccessStatusCode)
                {
                    if (string.IsNullOrEmpty(responseString))
                    {
                        result.ErrorMessage = $"Facebook returned HTTP {fbResponse.StatusCode} with empty response. Video may be too large or format not supported.";
                    }
                    else
                    {
                        var friendlyTitle = SocialErrorMapper.MapFacebookError(responseString, out var userAction);
                        result.ErrorMessage = $"{friendlyTitle}: {userAction}";
                    }
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

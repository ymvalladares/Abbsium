using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Entitys;
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

                if (acc.ExpiresAt <= DateTime.UtcNow.AddMinutes(5) && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    await RefreshTikTokAccessToken(acc);
                }

                var videoBytes = request.VideoUrl.StartsWith("data:")
                    ? ExtractBase64Media(request.VideoUrl)
                    : await DownloadMediaAsync(request.VideoUrl);

                _logger.LogInformation("Uploading video to TikTok: {Size} bytes", videoBytes.Length);

                var caption = request.Caption ?? request.Title ?? "";

                var (publishId, errorMessage) = await UploadVideoToTikTok(
                    acc.AccessToken,
                    videoBytes,
                    caption,
                    request.IsShort);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    result.ErrorMessage = errorMessage;
                    return result;
                }

                var (isPublished, statusError) = await WaitForPublishStatus(
                    acc.AccessToken,
                    publishId!);

                if (!isPublished)
                {
                    result.ErrorMessage = statusError ?? "Video processing timed out. Please check your TikTok account.";
                    return result;
                }

                result.Success = true;
                result.PostId = publishId;
                result.PostUrl = $"https://www.tiktok.com/video/{publishId}";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to TikTok for user {UserId}", userId);
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<(string? PublishId, string? ErrorMessage)> UploadVideoToTikTok(string accessToken, byte[] videoBytes, string description, bool isShort)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");

            var sessionId = await InitiateUpload(client, accessToken, videoBytes.Length, description, isShort);
            if (string.IsNullOrEmpty(sessionId))
            {
                return (null, "Error initiating TikTok upload. Please ensure the video is MP4 with a duration between 3 and 10 minutes.");
            }

            var uploadSuccess = await UploadVideoChunks(client, accessToken, sessionId, videoBytes);
            if (!uploadSuccess)
            {
                return (null, "Error uploading the video to TikTok. Please ensure it is MP4 format and under 10 minutes.");
            }

            var publishId = await CompleteUpload(client, accessToken, sessionId);
            if (string.IsNullOrEmpty(publishId))
            {
                return (null, "Error completing TikTok upload. Please try again.");
            }

            return (publishId, null);
        }

        private async Task<string?> InitiateUpload(HttpClient client, string accessToken, long videoSize, string description, bool isShort)
        {
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
                _logger.LogWarning("TikTok upload init failed: {StatusCode} {Response}", response.StatusCode, responseString);
                var mapped = SocialErrorMapper.MapTikTokError(responseString, out var userAction);
                _logger.LogWarning("TikTok mapped error: {Mapped} - {Action}", mapped, userAction);
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("session_id", out var sessionIdElem))
                {
                    var sessionId = sessionIdElem.GetString();
                    _logger.LogInformation("TikTok upload initiated with session_id: {SessionId}", sessionId);
                    return sessionId;
                }

                _logger.LogWarning("TikTok init response missing session_id: {Response}", responseString);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TikTok init response: {Response}", responseString);
                return null;
            }
        }

        private async Task<bool> UploadVideoChunks(HttpClient client, string accessToken, string sessionId, byte[] videoBytes)
        {
            var url = $"https://open.tiktokapis.com/v2/post/publish/content/upload/?session_id={sessionId}";

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new ByteArrayContent(videoBytes)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");

            var response = await client.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TikTok video upload failed: {StatusCode} {Response}", response.StatusCode, responseString);
                return false;
            }

            return true;
        }

        private async Task<string?> CompleteUpload(HttpClient client, string accessToken, string sessionId)
        {
            var url = "https://open.tiktokapis.com/v2/post/publish/content/fetch/";

            var body = new { session_id = sessionId };
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
                _logger.LogWarning("TikTok complete upload failed: {StatusCode} {Response}", response.StatusCode, responseString);
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("publish_id", out var publishIdElem))
                {
                    var publishId = publishIdElem.GetString();
                    _logger.LogInformation("TikTok upload completed with publish_id: {PublishId}", publishId);
                    return publishId;
                }

                _logger.LogWarning("TikTok complete response missing publish_id: {Response}", responseString);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing TikTok complete response: {Response}", responseString);
                return null;
            }
        }

        private async Task<(bool IsPublished, string? Error)> WaitForPublishStatus(string accessToken, string publishId)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");
            var maxAttempts = 60;
            var delayMs = 5000;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                await Task.Delay(delayMs);

                var url = "https://open.tiktokapis.com/v2/post/publish/status/fetch/";

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
                    _logger.LogWarning("TikTok status check failed: {Response}", responseString);
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("status", out var statusElem))
                        {
                            var status = statusElem.GetString();

                            if (status == "PUBLISH_COMPLETE")
                            {
                                _logger.LogInformation("TikTok publish {PublishId} completed successfully", publishId);
                                return (true, null);
                            }

                            if (status == "FAILED")
                            {
                                var errorMsg = ExtractTikTokErrorMessage(responseString) ?? "Video processing failed";
                                _logger.LogWarning("TikTok publish {PublishId} failed: {Error}", publishId, errorMsg);
                                return (false, errorMsg);
                            }

                            if (status == "PROCESSING_UPLOAD" || status == "PROCESSING_DEFAULT" || status == "PUBLISHING")
                            {
                                _logger.LogInformation("TikTok publish {PublishId} status: {Status} (attempt {Attempt}/{Max})", publishId, status, attempt + 1, maxAttempts);
                                continue;
                            }

                            _logger.LogInformation("TikTok publish {PublishId} status: {Status} (attempt {Attempt}/{Max})", publishId, status, attempt + 1, maxAttempts);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing TikTok status response: {Response}", responseString);
                }
            }

            _logger.LogWarning("TikTok publish {PublishId} timed out after {MaxAttempts} attempts", publishId, maxAttempts);
            return (false, "Video processing timed out. The video may still be processing on TikTok. Please check your account.");
        }

        private string? ExtractTikTokErrorMessage(string responseString)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var message))
                    {
                        return message.GetString();
                    }
                    if (error.TryGetProperty("code", out var code))
                    {
                        return $"TikTok error code: {code.GetInt32()}";
                    }
                }

                if (root.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("status", out var status))
                {
                    return $"Status: {status.GetString()}";
                }
            }
            catch { }

            return responseString;
        }

        private async Task RefreshTikTokAccessToken(SocialAccount acc)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = "https://open.tiktokapis.com/v2/oauth/token/";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_key"] = _config["TikTok:ClientId"],
                    ["client_secret"] = _config["TikTok:ClientSecret"],
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = acc.RefreshToken
                });

                var response = await client.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("TikTok token refresh failed: {StatusCode} {Response}", response.StatusCode, errorJson);
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("access_token", out var at))
                    {
                        acc.AccessToken = at.GetString()!;
                        acc.ExpiresAt = DateTime.UtcNow.AddSeconds(
                            data.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400
                        );
                        if (data.TryGetProperty("refresh_token", out var rt) && rt.GetString() is string newRt)
                        {
                            acc.RefreshToken = newRt;
                        }
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("TikTok token refreshed for account {AccountId}", acc.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error refreshing TikTok token for account {AccountId}", acc.Id);
            }
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Entitys;
using Server.Models.SocialMedia.Enums;
using Server.Services.SocialMedia.Base;
using Server.Services.SocialMedia.Interfaces;
using System.Text.Json;

namespace Server.Services.SocialMedia.Implementations
{
    public class TikTokAuthService : SocialServiceBase, ISocialAuthService
    {
        public SocialPlatform Platform => SocialPlatform.TikTok;
        protected override string ProviderName => "tiktok";

        public TikTokAuthService(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<TikTokAuthService> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public string GetAuthorizationUrl(string userId)
        {
            var state = GenerateStateToken(userId);
            var redirectUri = _config["TikTok:RedirectUri"];

            return "https://www.tiktok.com/v2/auth/authorize/" +
                $"?client_key={_config["TikTok:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=user.info.basic,video.upload,video.publish" +
                $"&response_type=code";
        }

        public async Task HandleCallbackAsync(string code, string state, string userId)
        {
            var tokenResponse = await ExchangeCodeForToken(code);
            var (openId, displayName) = await GetUserInfo(tokenResponse.AccessToken);

            var existingAccount = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "tiktok");

            if (existingAccount != null)
            {
                existingAccount.AccessToken = tokenResponse.AccessToken;
                existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                existingAccount.Scope = "user.info.basic,video.upload,video.publish";
                existingAccount.IsActive = true;
                existingAccount.ProviderAccountId = openId;
                existingAccount.AccountName = displayName;
                existingAccount.LastRefreshedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SocialAccounts.Add(new SocialAccount
                {
                    UserId = userId,
                    Provider = "tiktok",
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    Scope = "user.info.basic,video.upload,video.publish",
                    IsActive = true,
                    ProviderAccountId = openId,
                    AccountName = displayName,
                    LastRefreshedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("TikTok account saved for user {UserId} with display name {DisplayName}", userId, displayName);
        }

        public async Task RefreshTokenAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) throw new Exception("TikTok account not found");
            if (string.IsNullOrEmpty(acc.RefreshToken)) throw new Exception("No refresh token available");

            var newToken = await RefreshAccessToken(acc.RefreshToken);
            acc.AccessToken = newToken.AccessToken;
            acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
            acc.LastRefreshedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsTokenValidAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) return false;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = "https://open.tiktokapis.com/v2/user/info/?fields=open_id";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", acc.AccessToken);

                var response = await client.SendAsync(request);
                
                if (response.IsSuccessStatusCode) return true;

                // Token expirado, intentar refrescar
                if (!string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshAccessToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                    acc.LastRefreshedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task DisconnectAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc != null)
            {
                _db.SocialAccounts.Remove(acc);
                await _db.SaveChangesAsync();
            }
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/oauth/token/";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = _config["TikTok:ClientId"],
                ["client_secret"] = _config["TikTok:ClientSecret"],
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = _config["TikTok:RedirectUri"],
                ["code"] = code
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var accessToken = data.TryGetProperty("access_token", out var atElem)
                    ? atElem.GetString() ?? throw new Exception("Missing access_token")
                    : throw new Exception($"TikTok token response missing access_token: {jsonString}");

                var expiresIn = data.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;
                var refreshToken = data.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                return (accessToken, refreshToken, expiresIn);
            }

            throw new Exception($"TikTok token response invalid: {jsonString}");
        }

        private async Task<(string OpenId, string DisplayName)> GetUserInfo(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var openId = data.TryGetProperty("open_id", out var openIdElem)
                    ? openIdElem.GetString() ?? throw new Exception("Missing TikTok open_id")
                    : throw new Exception("TikTok open_id not found");

                var displayName = "TikTok Account";
                if (data.TryGetProperty("user", out var user) &&
                    user.TryGetProperty("display_name", out var displayNameElem))
                {
                    displayName = displayNameElem.GetString() ?? "TikTok Account";
                }

                return (openId, displayName);
            }

            throw new Exception("Failed to get TikTok user info");
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshAccessToken(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/oauth/token/";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = _config["TikTok:ClientId"],
                ["client_secret"] = _config["TikTok:ClientSecret"],
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var accessToken = data.TryGetProperty("access_token", out var atElem)
                    ? atElem.GetString() ?? throw new Exception("Missing access_token")
                    : throw new Exception($"TikTok token refresh failed: {json}");

                var expiresIn = data.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;
                var newRefreshToken = data.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                return (accessToken, newRefreshToken, expiresIn);
            }

            throw new Exception($"TikTok token refresh response invalid: {json}");
        }
    }
}

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
    public class InstagramAuthService : SocialServiceBase, ISocialAuthService
    {
        public SocialPlatform Platform => SocialPlatform.Instagram;
        protected override string ProviderName => "instagram";

        public InstagramAuthService(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<InstagramAuthService> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public string GetAuthorizationUrl(string userId)
        {
            var state = GenerateStateToken(userId);

            return "https://api.instagram.com/oauth/authorize" +
                $"?client_id={_config["Instagram:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(_config["Instagram:RedirectUri"])}" +
                $"&state={state}" +
                $"&scope=user_profile,user_media,instagram_basic,instagram_content_publish,instagram_manage_comments" +
                $"&response_type=code";
        }

        public async Task HandleCallbackAsync(string code, string state, string userId)
        {
            var tokenResponse = await ExchangeCodeForToken(code);
            var username = await GetInstagramUsername(tokenResponse.AccessToken);

            var existingAccount = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "instagram");

            if (existingAccount != null)
            {
                existingAccount.AccessToken = tokenResponse.AccessToken;
                existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                existingAccount.Scope = "user_profile,user_media,instagram_basic,instagram_content_publish";
                existingAccount.IsActive = true;
                existingAccount.ProviderAccountId = tokenResponse.UserId;
                existingAccount.AccountName = username;
            }
            else
            {
                _db.SocialAccounts.Add(new SocialAccount
                {
                    UserId = userId,
                    Provider = "instagram",
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    Scope = "user_profile,user_media,instagram_basic,instagram_content_publish",
                    IsActive = true,
                    ProviderAccountId = tokenResponse.UserId,
                    AccountName = username
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Instagram account saved for user {UserId} with username {Username}", userId, username);
        }

        public async Task RefreshTokenAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) throw new Exception("Instagram account not found");
            if (string.IsNullOrEmpty(acc.RefreshToken)) throw new Exception("No refresh token available");

            var newToken = await RefreshAccessToken(acc.RefreshToken);
            acc.AccessToken = newToken.AccessToken;
            acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsTokenValidAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) return false;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var res = await client.GetAsync(
                    $"https://graph.instagram.com/me?access_token={acc.AccessToken}&fields=id,username");
                
                if (res.IsSuccessStatusCode) return true;

                // Token expirado, intentar refrescar
                if (!string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshAccessToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
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

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn, string UserId)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://api.instagram.com/oauth/access_token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _config["Instagram:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["Instagram:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _config["Instagram:RedirectUri"]),
                new KeyValuePair<string, string>("code", code)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"Instagram token response missing access_token: {jsonString}");

            var userId = root.TryGetProperty("user_id", out var uidElem)
                ? uidElem.GetString() ?? throw new Exception("Missing user_id")
                : throw new Exception($"Instagram token response missing user_id: {jsonString}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return (accessToken, refreshToken, expiresIn, userId);
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshAccessToken(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://graph.instagram.com/refresh_access_token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "ig_refresh_token"),
                new KeyValuePair<string, string>("client_id", _config["Instagram:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["Instagram:ClientSecret"]),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"Instagram token refresh failed: {json}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;
            var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return (accessToken, newRefreshToken, expiresIn);
        }

        private async Task<string> GetInstagramUsername(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.instagram.com/me?fields=username&access_token={accessToken}";

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Instagram username: {Response}", responseString);
                return "Instagram Account";
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("username", out var usernameElem))
            {
                return usernameElem.GetString() ?? "Instagram Account";
            }

            return "Instagram Account";
        }
    }
}

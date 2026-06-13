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
    public class YouTubeAuthService : SocialServiceBase, ISocialAuthService
    {
        public SocialPlatform Platform => SocialPlatform.YouTube;
        protected override string ProviderName => "youtube";

        public YouTubeAuthService(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<YouTubeAuthService> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public string GetAuthorizationUrl(string userId)
        {
            var state = GenerateStateToken(userId);
            var redirectUri = _config["YouTube:RedirectUri"];

            return "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={_config["YouTube:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.force-ssl" +
                $"&response_type=code" +
                $"&access_type=offline" +
                $"&prompt=consent";
        }

        public async Task HandleCallbackAsync(string code, string state, string userId)
        {
            var tokenResponse = await ExchangeCodeForToken(code);
            var (channelId, channelName) = await GetChannelInfo(tokenResponse.AccessToken);

            var existingAccount = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "youtube");

            if (existingAccount != null)
            {
                existingAccount.AccessToken = tokenResponse.AccessToken;
                existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                existingAccount.Scope = "youtube.upload,youtube.force-ssl";
                existingAccount.IsActive = true;
                existingAccount.ProviderAccountId = channelId;
                existingAccount.AccountName = channelName;
                existingAccount.LastRefreshedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SocialAccounts.Add(new SocialAccount
                {
                    UserId = userId,
                    Provider = "youtube",
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    Scope = "youtube.upload,youtube.force-ssl",
                    IsActive = true,
                    ProviderAccountId = channelId,
                    AccountName = channelName,
                    LastRefreshedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("YouTube account saved for user {UserId} with channel {ChannelName}", userId, channelName);
        }

        public async Task RefreshTokenAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) throw new Exception("YouTube account not found");
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
                var res = await client.GetAsync(
                    $"https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true&access_token={acc.AccessToken}");
                
                if (res.IsSuccessStatusCode) return true;

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
            var url = "https://oauth2.googleapis.com/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _config["YouTube:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["YouTube:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _config["YouTube:RedirectUri"]),
                new KeyValuePair<string, string>("code", code)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"YouTube token response missing access_token: {jsonString}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return (accessToken, refreshToken, expiresIn);
        }

        private async Task<(string ChannelId, string ChannelName)> GetChannelInfo(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://www.googleapis.com/youtube/v3/channels?part=id,snippet&mine=true";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var firstItem = items[0];
                var channelId = firstItem.TryGetProperty("id", out var idElem)
                    ? idElem.GetString() ?? throw new Exception("Missing YouTube channel id")
                    : throw new Exception("YouTube channel id not found");

                var channelName = "YouTube Channel";
                if (firstItem.TryGetProperty("snippet", out var snippet) &&
                    snippet.TryGetProperty("title", out var titleElem))
                {
                    channelName = titleElem.GetString() ?? "YouTube Channel";
                }

                return (channelId, channelName);
            }

            throw new Exception("No YouTube channel found");
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshAccessToken(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://oauth2.googleapis.com/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _config["YouTube:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["YouTube:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"YouTube token refresh failed: {json}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            var newRefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return (accessToken, newRefreshToken, expiresIn);
        }
    }
}

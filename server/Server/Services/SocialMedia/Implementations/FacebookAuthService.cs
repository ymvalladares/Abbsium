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
    public class FacebookAuthService : SocialServiceBase, ISocialAuthService
    {
        public SocialPlatform Platform => SocialPlatform.Facebook;
        protected override string ProviderName => "facebook";

        public FacebookAuthService(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<FacebookAuthService> logger)
            : base(db, config, httpClientFactory, logger)
        {
        }

        public string GetAuthorizationUrl(string userId)
        {
            var state = GenerateStateToken(userId);
            var redirectUri = _config["Facebook:RedirectUri"];

            return "https://www.facebook.com/v19.0/dialog/oauth" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=email,public_profile,pages_show_list,pages_manage_posts,pages_read_engagement,pages_manage_engagement,instagram_basic,instagram_content_publish";
        }

        public async Task HandleCallbackAsync(string code, string state, string userId)
        {
            var tokenResponse = await ExchangeCodeForToken(code);
            var fbProfileId = await GetProfileId(tokenResponse.AccessToken);
            var pages = await GetPages(tokenResponse.AccessToken);

            var pageId = pages.Count > 0 ? pages[0].Id : fbProfileId;
            var pageName = pages.Count > 0 ? pages[0].Name : "Facebook Page";

            var existingAccount = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook");

            if (existingAccount != null)
            {
                existingAccount.AccessToken = tokenResponse.AccessToken;
                existingAccount.RefreshToken = tokenResponse.LongLivedAccessToken ?? string.Empty;
                existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                existingAccount.Scope = "public_profile,email,pages_show_list,pages_manage_posts,pages_read_engagement";
                existingAccount.IsActive = true;
                existingAccount.ProviderAccountId = pageId;
                existingAccount.AccountName = pageName;
                existingAccount.LastRefreshedAt = DateTime.UtcNow;
            }
            else
            {
                _db.SocialAccounts.Add(new SocialAccount
                {
                    UserId = userId,
                    Provider = "facebook",
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.LongLivedAccessToken ?? string.Empty,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                    Scope = "public_profile,email,pages_show_list,pages_manage_posts,pages_read_engagement",
                    IsActive = true,
                    ProviderAccountId = pageId,
                    AccountName = pageName,
                    LastRefreshedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("Facebook account saved for user {UserId} with page {PageName}", userId, pageName);
        }

        public async Task RefreshTokenAsync(string userId)
        {
            var acc = await GetSocialAccountAsync(userId);
            if (acc == null) throw new Exception("Facebook account not found");

            var newToken = await RefreshAccessToken(acc.AccessToken);
            acc.AccessToken = newToken.AccessToken;
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
                var res = await client.GetAsync($"https://graph.facebook.com/me?access_token={acc.AccessToken}");
                
                if (res.IsSuccessStatusCode) return true;

                // Token expirado, intentar refrescar
                if (!string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshAccessToken(acc.AccessToken);
                    acc.AccessToken = newToken.AccessToken;
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

        private async Task<(string AccessToken, string? LongLivedAccessToken, int ExpiresIn)> ExchangeCodeForToken(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var redirectUri = _config["Facebook:RedirectUri"];

            var url = $"https://graph.facebook.com/v19.0/oauth/access_token" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&client_secret={_config["Facebook:ClientSecret"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&code={code}";

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Facebook token exchange failed: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"Facebook token exchange failed: {responseString}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;
            var longLived = root.TryGetProperty("long_lived_token", out var ll) ? ll.GetString() : null;

            return (accessToken, longLived, expiresIn);
        }

        private async Task<string> GetProfileId(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/me?fields=id&access_token={accessToken}";
            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to get Facebook profile: {responseString}");

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return root.TryGetProperty("id", out var idElem)
                ? idElem.GetString() ?? throw new Exception("Missing Facebook user id")
                : throw new Exception($"Facebook profile response missing id: {responseString}");
        }

        private async Task<List<(string Id, string Name, string AccessToken)>> GetPages(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/me/accounts?fields=id,name,access_token&access_token={accessToken}";

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new List<(string, string, string)>();

            var pages = new List<(string Id, string Name, string AccessToken)>();
            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                foreach (var page in data.EnumerateArray())
                {
                    var id = page.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var name = page.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var pageToken = page.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(pageToken))
                    {
                        pages.Add((id, name, pageToken));
                    }
                }
            }

            return pages;
        }

        private async Task<(string AccessToken, int ExpiresIn)> RefreshAccessToken(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/oauth/access_token" +
                $"?grant_type=fb_exchange_token" +
                $"&client_id={_config["Facebook:ClientId"]}" +
                $"&client_secret={_config["Facebook:ClientSecret"]}" +
                $"&fb_exchange_token={accessToken}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var newToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"Facebook token refresh failed: {json}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000;

            return (newToken, expiresIn);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;
using Server.ModelDTO;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    public class SocialAuthController : Base_Control_Api
    {
        private readonly DbContext_app _db;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SocialAuthController> _logger;

        public SocialAuthController(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger<SocialAuthController> logger)
        {
            _db = db;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // ================= FACEBOOK =================
        [Authorize]
        [HttpPost("facebook/connect")]
        public IActionResult ConnectFacebook()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var state = GenerateStateToken(userId);
            var redirectUri = _config["Facebook:RedirectUri"];

            var url =
                "https://www.facebook.com/v19.0/dialog/oauth" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=email,public_profile,pages_show_list,pages_manage_posts,pages_read_engagement";

            _logger.LogInformation("Facebook connect initiated for user {UserId}, redirect_uri: {RedirectUri}", userId, redirectUri);
            return Ok(new { url });
        }

        [HttpGet("facebook/callback")]
        public async Task<IActionResult> FacebookCallback()
        {
            var code = Request.Query["code"].ToString();
            var state = Request.Query["state"].ToString();
            var error = Request.Query["error"].ToString();
            var errorDescription = Request.Query["error_description"].ToString();

            var clientUrl = _config["ClientUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{clientUrl}/auth/social-callback";

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Facebook callback error: {Error} - {Description}", error, errorDescription);
                return Redirect($"{callbackUrl}?status=error&provider=facebook&error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("Facebook callback failed: missing code or state");
                return Redirect($"{callbackUrl}?status=error&provider=facebook&error=missing_params");
            }

            var userId = ValidateStateToken(state);
            if (userId == null)
            {
                _logger.LogWarning("Facebook callback failed: invalid state token");
                return Redirect($"{callbackUrl}?status=error&provider=facebook&error=invalid_state");
            }

            try
            {
                var tokenResponse = await ExchangeFacebookCode(code);
                var pages = await GetFacebookPages(tokenResponse.AccessToken);

                var pageId = pages.Count > 0 ? pages[0].Id : null;
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
                        AccountName = pageName
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Facebook account saved for user {UserId} with page {PageName}", userId, pageName);

                return Redirect($"{callbackUrl}?status=success&provider=facebook");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting Facebook account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=facebook&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? LongLivedAccessToken, int ExpiresIn)> ExchangeFacebookCode(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var redirectUri = _config["Facebook:RedirectUri"];

            _logger.LogInformation("Exchanging Facebook code, redirect_uri: {RedirectUri}", redirectUri);

            var url =
                $"https://graph.facebook.com/v19.0/oauth/access_token" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&client_secret={_config["Facebook:ClientSecret"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&code={code}";

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Facebook token exchange failed: {StatusCode} - {Response}", response.StatusCode, responseString);
                throw new Exception($"Facebook token exchange failed: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenElem))
            {
                var errorMsg = root.TryGetProperty("error", out var errorElem)
                    ? errorElem.GetRawText()
                    : responseString;
                _logger.LogError("Facebook token response missing access_token: {Response}", responseString);
                throw new Exception($"Facebook token exchange failed: {errorMsg}");
            }

            var accessToken = accessTokenElem.GetString() ?? throw new Exception("Missing access_token");
            var expiresIn = root.TryGetProperty("expires_in", out var expiresInElem) ? expiresInElem.GetInt32() : 5184000;
            var longLivedToken = root.TryGetProperty("long_lived_token", out var llToken) ? llToken.GetString() : null;

            _logger.LogInformation("Facebook token exchanged successfully, expires_in: {ExpiresIn}", expiresIn);
            return (accessToken, longLivedToken, expiresIn);
        }

        private async Task<string> GetFacebookProfileId(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/me?fields=id&access_token={accessToken}";

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to get Facebook profile: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            return root.TryGetProperty("id", out var idElem)
                ? idElem.GetString() ?? throw new Exception("Missing Facebook user id")
                : throw new Exception($"Facebook profile response missing id field: {responseString}");
        }

        private async Task<List<(string Id, string Name, string AccessToken)>> GetFacebookPages(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/me/accounts?fields=id,name,access_token&access_token={accessToken}";

            _logger.LogInformation("Requesting Facebook pages: {Url}", url.Replace(accessToken, "***"));

            var response = await client.GetAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Facebook pages response status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Facebook pages response body: {Response}", responseString);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Facebook pages: {Response}", responseString);
                return new List<(string, string, string)>();
            }

            var pages = new List<(string Id, string Name, string AccessToken)>();
            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                _logger.LogInformation("Facebook pages data array length: {Count}", data.GetArrayLength());
                
                foreach (var page in data.EnumerateArray())
                {
                    var id = page.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    var name = page.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    var pageToken = page.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;
                    
                    _logger.LogInformation("Facebook page: id={Id}, name={Name}, hasToken={HasToken}", id, name, !string.IsNullOrEmpty(pageToken));
                    
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(pageToken))
                    {
                        pages.Add((id, name, pageToken));
                    }
                }
            }
            else
            {
                _logger.LogWarning("No 'data' property found in Facebook pages response");
            }

            _logger.LogInformation("Found {Count} Facebook pages", pages.Count);
            return pages;
        }

        // ================= INSTAGRAM =================
        [Authorize]
        [HttpPost("instagram/connect")]
        public IActionResult ConnectInstagram()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var state = GenerateStateToken(userId);

            var url =
                "https://api.instagram.com/oauth/authorize" +
                $"?client_id={_config["Instagram:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(_config["Instagram:RedirectUri"])}" +
                $"&state={state}" +
                $"&scope=user_profile,user_media" +
                $"&response_type=code";

            return Ok(new { url });
        }

        [HttpGet("instagram/callback")]
        public async Task<IActionResult> InstagramCallback()
        {
            var code = Request.Query["code"].ToString();
            var state = Request.Query["state"].ToString();
            var error = Request.Query["error"].ToString();
            var errorDescription = Request.Query["error_description"].ToString();

            var clientUrl = _config["ClientUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{clientUrl}/auth/social-callback";

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Instagram callback error: {Error}", error);
                return Redirect($"{callbackUrl}?status=error&provider=instagram&error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                _logger.LogWarning("Instagram callback failed: missing code or state");
                return Redirect($"{callbackUrl}?status=error&provider=instagram&error=missing_params");
            }

            var userId = ValidateStateToken(state);
            if (userId == null)
            {
                _logger.LogWarning("Instagram callback failed: invalid state token");
                return Redirect($"{callbackUrl}?status=error&provider=instagram&error=invalid_state");
            }

            try
            {
                var tokenResponse = await ExchangeInstagramCode(code);

                var existingAccount = await _db.SocialAccounts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "instagram");

                if (existingAccount != null)
                {
                    existingAccount.AccessToken = tokenResponse.AccessToken;
                    existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                    existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingAccount.Scope = "user_profile,user_media";
                    existingAccount.IsActive = true;
                    existingAccount.ProviderAccountId = tokenResponse.UserId;
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
                        Scope = "user_profile,user_media",
                        IsActive = true,
                        ProviderAccountId = tokenResponse.UserId
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Instagram account saved for user {UserId}", userId);

                return Redirect($"{callbackUrl}?status=success&provider=instagram");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting Instagram account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=instagram&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn, string UserId)> ExchangeInstagramCode(string code)
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

        // ================= YOUTUBE =================
        [Authorize]
        [HttpPost("youtube/connect")]
        public IActionResult ConnectYouTube()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var state = GenerateStateToken(userId);
            var redirectUri = _config["YouTube:RedirectUri"];

            var url =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={_config["YouTube:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.force-ssl" +
                $"&response_type=code" +
                $"&access_type=offline" +
                $"&prompt=consent";

            _logger.LogInformation("YouTube connect initiated for user {UserId}", userId);
            return Ok(new { url });
        }

        [HttpGet("youtube/callback")]
        public async Task<IActionResult> YouTubeCallback()
        {
            var code = Request.Query["code"].ToString();
            var state = Request.Query["state"].ToString();
            var error = Request.Query["error"].ToString();

            var clientUrl = _config["ClientUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{clientUrl}/auth/social-callback";

            if (!string.IsNullOrEmpty(error))
            {
                return Redirect($"{callbackUrl}?status=error&provider=youtube&error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Redirect($"{callbackUrl}?status=error&provider=youtube&error=missing_params");
            }

            var userId = ValidateStateToken(state);
            if (userId == null)
            {
                return Redirect($"{callbackUrl}?status=error&provider=youtube&error=invalid_state");
            }

            try
            {
                var tokenResponse = await ExchangeYouTubeCode(code);
                var (channelId, channelName) = await GetYouTubeChannelInfo(tokenResponse.AccessToken);

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
                        AccountName = channelName
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("YouTube account saved for user {UserId}", userId);

                return Redirect($"{callbackUrl}?status=success&provider=youtube");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting YouTube account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=youtube&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> ExchangeYouTubeCode(string code)
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

        private async Task<(string ChannelId, string ChannelName)> GetYouTubeChannelInfo(string accessToken)
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

        // ================= TIKTOK =================
        [Authorize]
        [HttpPost("tiktok/connect")]
        public IActionResult ConnectTikTok()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var state = GenerateStateToken(userId);

            var url =
                "https://www.tiktok.com/v2/auth/authorize/" +
                $"?client_key={_config["TikTok:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(_config["TikTok:RedirectUri"])}" +
                $"&state={state}" +
                $"&scope=user.info.basic,video.upload" +
                $"&response_type=code";

            _logger.LogInformation("TikTok connect initiated for user {UserId}", userId);
            return Ok(new { url });
        }

        [HttpGet("tiktok/callback")]
        public async Task<IActionResult> TikTokCallback()
        {
            var code = Request.Query["code"].ToString();
            var state = Request.Query["state"].ToString();
            var error = Request.Query["error"].ToString();

            var clientUrl = _config["ClientUrl"] ?? "http://localhost:3000";
            var callbackUrl = $"{clientUrl}/auth/social-callback";

            if (!string.IsNullOrEmpty(error))
            {
                return Redirect($"{callbackUrl}?status=error&provider=tiktok&error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Redirect($"{callbackUrl}?status=error&provider=tiktok&error=missing_params");
            }

            var userId = ValidateStateToken(state);
            if (userId == null)
            {
                return Redirect($"{callbackUrl}?status=error&provider=tiktok&error=invalid_state");
            }

            try
            {
                var tokenResponse = await ExchangeTikTokCode(code);
                var openId = await GetTikTokOpenId(tokenResponse.AccessToken);

                var existingAccount = await _db.SocialAccounts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "tiktok");

                if (existingAccount != null)
                {
                    existingAccount.AccessToken = tokenResponse.AccessToken;
                    existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                    existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingAccount.Scope = "user.info.basic,video.upload";
                    existingAccount.IsActive = true;
                    existingAccount.ProviderAccountId = openId;
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
                        Scope = "user.info.basic,video.upload",
                        IsActive = true,
                        ProviderAccountId = openId
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("TikTok account saved for user {UserId}", userId);

                return Redirect($"{callbackUrl}?status=success&provider=tiktok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting TikTok account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=tiktok&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> ExchangeTikTokCode(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/oauth/token/";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_key", _config["TikTok:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["TikTok:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _config["TikTok:RedirectUri"]),
                new KeyValuePair<string, string>("code", code)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"TikTok token response missing access_token: {jsonString}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

            return (accessToken, refreshToken, expiresIn);
        }

        private async Task<string> GetTikTokOpenId(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/user/info/?fields=open_id";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("open_id", out var openIdElem))
            {
                return openIdElem.GetString() ?? throw new Exception("Missing TikTok open_id");
            }

            throw new Exception("No TikTok open_id found");
        }

        // ================= STATUS =================
        [Authorize]
        [HttpGet("status")]
        public IActionResult Status()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var accounts = _db.SocialAccounts
                .Where(x => x.UserId == userId && x.IsActive)
                .ToList();

            var result = accounts.Select(x => new SocialAccountDTO
            {
                Id = x.Id,
                Provider = x.Provider,
                Connected = true,
                IsActive = x.IsActive,
                ExpiresAt = x.ExpiresAt,
                CreatedAt = x.CreatedAt,
                ProviderAccountId = x.ProviderAccountId,
                Scope = x.Scope,
                AccountName = x.AccountName
            }).ToList();

            return Ok(result);
        }

        // ================= DISCONNECT =================
        [Authorize]
        [HttpDelete("disconnect/{provider}")]
        public async Task<IActionResult> Disconnect(string provider)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider.ToLower() == provider.ToLower());

            if (acc == null)
                return NotFound(new { error = $"{provider} account not found" });

            _db.SocialAccounts.Remove(acc);
            await _db.SaveChangesAsync();

            _logger.LogInformation("{Provider} account disconnected and deleted for user {UserId}", provider, userId);
            return Ok(new { message = $"{provider} disconnected successfully" });
        }

        // ================= PAGES =================
        [Authorize]
        [HttpGet("facebook/pages")]
        public async Task<IActionResult> GetPages()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            try
            {
                var pages = await GetFacebookPages(acc.AccessToken);
                
                var pageId = pages.Count > 0 ? pages[0].Id : null;
                var pageName = pages.Count > 0 ? pages[0].Name : "Facebook Page";
                
                acc.ProviderAccountId = pageId;
                acc.AccountName = pageName;
                await _db.SaveChangesAsync();

                return Ok(new { 
                    id = pageId,
                    name = pageName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Facebook pages for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to get pages", details = ex.Message });
            }
        }

        // ================= TOKEN REFRESH =================
        [Authorize]
        [HttpPost("refresh/{provider}")]
        public async Task<IActionResult> RefreshToken(string provider)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider.ToLower() == provider.ToLower() && x.IsActive);

            if (acc == null)
                return NotFound(new { error = $"{provider} account not found or not active" });

            try
            {
                if (provider.ToLower() == "facebook")
                {
                    var newToken = await RefreshFacebookToken(acc.AccessToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                }
                else if (provider.ToLower() == "instagram" && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshInstagramToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                }
                else if (provider.ToLower() == "youtube" && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshYouTubeToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                }
                else if (provider.ToLower() == "tiktok" && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshTikTokToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                }
                else
                {
                    return BadRequest(new { error = $"Token refresh not supported for {provider}" });
                }

                await _db.SaveChangesAsync();
                return Ok(new { message = "Token refreshed successfully", expiresAt = acc.ExpiresAt });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing {Provider} token for user {UserId}", provider, userId);
                acc.IsActive = false;
                await _db.SaveChangesAsync();
                return StatusCode(500, new { error = "Failed to refresh token", details = ex.Message });
            }
        }

        private async Task<(string AccessToken, int ExpiresIn)> RefreshFacebookToken(string accessToken)
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

            if (!root.TryGetProperty("access_token", out var atElem))
            {
                _logger.LogError("Facebook token refresh response missing access_token: {Response}", json);
                throw new Exception($"Facebook token refresh failed: {json}");
            }

            return (
                atElem.GetString() ?? throw new Exception("Missing access_token"),
                root.TryGetProperty("expires_in", out var expElem) ? expElem.GetInt32() : 5184000
            );
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshInstagramToken(string refreshToken)
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

            if (!root.TryGetProperty("access_token", out var atElem))
            {
                _logger.LogError("Instagram token refresh response missing access_token: {Response}", json);
                throw new Exception($"Instagram token refresh failed: {json}");
            }

            return (
                atElem.GetString() ?? throw new Exception("Missing access_token"),
                root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000
            );
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshYouTubeToken(string refreshToken)
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

            if (!root.TryGetProperty("access_token", out var atElem))
            {
                _logger.LogError("YouTube token refresh response missing access_token: {Response}", json);
                throw new Exception($"YouTube token refresh failed: {json}");
            }

            return (
                atElem.GetString() ?? throw new Exception("Missing access_token"),
                root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600
            );
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn)> RefreshTikTokToken(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/oauth/token/";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_key", _config["TikTok:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["TikTok:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var atElem))
            {
                _logger.LogError("TikTok token refresh response missing access_token: {Response}", json);
                throw new Exception($"TikTok token refresh failed: {json}");
            }

            return (
                atElem.GetString() ?? throw new Exception("Missing access_token"),
                root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
                root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400
            );
        }

        // ================= CONNECTIONS CHECK =================
        [Authorize]
        [HttpGet("connections/check")]
        public async Task<IActionResult> CheckConnections()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var accounts = await _db.SocialAccounts
                .Where(x => x.UserId == userId && x.IsActive)
                .ToListAsync();

            var result = new List<object>();

            foreach (var acc in accounts)
            {
                bool isValid = false;
                bool wasRefreshed = false;

                if (acc.Provider == "facebook")
                {
                    isValid = await IsFacebookTokenValid(acc.AccessToken);
                }
                else if (acc.Provider == "instagram")
                {
                    isValid = await IsInstagramTokenValid(acc.AccessToken);
                    if (!isValid && !string.IsNullOrEmpty(acc.RefreshToken))
                    {
                        try
                        {
                            var newToken = await RefreshInstagramToken(acc.RefreshToken);
                            acc.AccessToken = newToken.AccessToken;
                            acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                            isValid = true;
                            wasRefreshed = true;
                        }
                        catch { }
                    }
                }
                else if (acc.Provider == "youtube")
                {
                    isValid = await IsYouTubeTokenValid(acc.AccessToken);
                    if (!isValid && !string.IsNullOrEmpty(acc.RefreshToken))
                    {
                        try
                        {
                            var newToken = await RefreshYouTubeToken(acc.RefreshToken);
                            acc.AccessToken = newToken.AccessToken;
                            acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                            isValid = true;
                            wasRefreshed = true;
                        }
                        catch { }
                    }
                }
                else if (acc.Provider == "tiktok")
                {
                    isValid = await IsTikTokTokenValid(acc.AccessToken);
                    if (!isValid && !string.IsNullOrEmpty(acc.RefreshToken))
                    {
                        try
                        {
                            var newToken = await RefreshTikTokToken(acc.RefreshToken);
                            acc.AccessToken = newToken.AccessToken;
                            acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                            isValid = true;
                            wasRefreshed = true;
                        }
                        catch { }
                    }
                }

                if (!isValid)
                {
                    _db.SocialAccounts.Remove(acc);
                    _logger.LogWarning("{Provider} token invalid for user {UserId}, account deleted", acc.Provider, userId);
                }
                else if (wasRefreshed)
                {
                    _logger.LogInformation("{Provider} token refreshed for user {UserId}", acc.Provider, userId);
                }

                result.Add(new SocialAccountDTO
                {
                    Id = acc.Id,
                    Provider = acc.Provider,
                    Connected = isValid,
                    IsActive = acc.IsActive,
                    ExpiresAt = acc.ExpiresAt,
                    CreatedAt = acc.CreatedAt,
                    ProviderAccountId = acc.ProviderAccountId,
                    Scope = acc.Scope,
                    AccountName = acc.AccountName
                });
            }

            await _db.SaveChangesAsync();
            return Ok(result);
        }

        private async Task<bool> IsFacebookTokenValid(string accessToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var res = await client.GetAsync(
                    $"https://graph.facebook.com/me?access_token={accessToken}");
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsInstagramTokenValid(string accessToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var res = await client.GetAsync(
                    $"https://graph.instagram.com/me?access_token={accessToken}&fields=id,username");
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsYouTubeTokenValid(string accessToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var res = await client.GetAsync(
                    $"https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true&access_token={accessToken}");
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsTikTokTokenValid(string accessToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "https://open.tiktokapis.com/v2/user/info/?fields=open_id");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var res = await client.SendAsync(request);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ================= TEST PROFILE =================
        [Authorize]
        [HttpGet("facebook/test-profile")]
        public async Task<IActionResult> TestFacebookProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return NotFound("Facebook not connected");

            return await GetFacebookProfile(acc.AccessToken);
        }

        private async Task<IActionResult> GetFacebookProfile(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/me" +
                      $"?fields=id,name,email,picture.width(200)" +
                      $"&access_token={accessToken}";

            var res = await client.GetAsync(url);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                return BadRequest(new { error = "Facebook API error", details = err });
            }

            var json = await res.Content.ReadAsStringAsync();
            return Ok(JsonDocument.Parse(json).RootElement);
        }

        // ================= POST TO FACEBOOK =================
        public class FacebookPostRequest
        {
            public string Message { get; set; }
            public string PhotoUrl { get; set; }
            public string Caption { get; set; }
            public string PageId { get; set; }
        }

        [Authorize]
        [HttpPost("facebook/post")]
        public async Task<IActionResult> PostToFacebook([FromBody] FacebookPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            if (string.IsNullOrEmpty(request.Message) && string.IsNullOrEmpty(request.PhotoUrl))
                return BadRequest("Message or PhotoUrl is required");

            try
            {
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

                _logger.LogInformation("Facebook post response: {StatusCode} - {Response}", fbResponse.StatusCode, responseString);

                if (!fbResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { error = "Facebook API error", details = responseString });
                }

                return Ok(new { status = "Published", response = responseString, pageId = pageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting to Facebook for user {UserId}", userId);
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("facebook/photo")]
        public async Task<IActionResult> PostPhotoToFacebook([FromBody] FacebookPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            if (string.IsNullOrEmpty(request.PhotoUrl))
                return BadRequest("PhotoUrl is required");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;
                var fbUrl = string.IsNullOrEmpty(pageId)
                    ? "https://graph.facebook.com/me/photos"
                    : $"https://graph.facebook.com/{pageId}/photos";

                var content = new MultipartFormDataContent();

                byte[] bytes;
                if (request.PhotoUrl.StartsWith("data:"))
                {
                    var base64Data = request.PhotoUrl.Substring(request.PhotoUrl.IndexOf(",") + 1);
                    bytes = Convert.FromBase64String(base64Data);
                }
                else if (request.PhotoUrl.StartsWith("http"))
                {
                    var imageResponse = await client.GetAsync(request.PhotoUrl);
                    imageResponse.EnsureSuccessStatusCode();
                    bytes = await imageResponse.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    return BadRequest("Invalid PhotoUrl format");
                }

                content.Add(new ByteArrayContent(bytes), "source", "photo.jpg");
                content.Add(new StringContent(request.Message ?? ""), "message");
                content.Add(new StringContent(acc.AccessToken), "access_token");

                var fbResponse = await client.PostAsync(fbUrl, content);
                var responseString = await fbResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Facebook photo post response: {StatusCode} - {Response}", fbResponse.StatusCode, responseString);

                if (!fbResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { error = "Facebook API error", details = responseString });
                }

                return Ok(new { status = "Photo published", response = responseString, pageId = pageId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting photo to Facebook for user {UserId}", userId);
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        // ================= HELPERS =================
        private string GenerateStateToken(string userId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var plainText = $"{userId}|{timestamp}";
            var key = _config["Jwt:Key"] ?? "fallback-key-for-state";
            var bytes = System.Text.Encoding.UTF8.GetBytes(plainText + key);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            var hashPart = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");
            var token = $"{plainText}|{hashPart}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(token))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string? ValidateStateToken(string state)
        {
            try
            {
                // Decode base64url state
                var padded = state.PadRight(state.Length + (4 - state.Length % 4) % 4, '=');
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace("-", "+").Replace("_", "/")));

                var parts = decoded.Split('|');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("State token has wrong number of parts: {Parts}", parts.Length);
                    return null;
                }

                var userId = parts[0];
                var timestamp = long.Parse(parts[1]);
                var providedHash = parts[2];

                var maxAge = TimeSpan.FromMinutes(15);
                var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                var age = DateTimeOffset.UtcNow - tokenTime;

                _logger.LogInformation("State token age: {Age} seconds for user {UserId}", age.TotalSeconds, userId);

                if (age > maxAge)
                {
                    _logger.LogWarning("State token expired: age={Age}", age.TotalSeconds);
                    return null;
                }

                var plainText = $"{userId}|{timestamp}";
                var key = _config["Jwt:Key"] ?? "fallback-key-for-state";
                var bytes = System.Text.Encoding.UTF8.GetBytes(plainText + key);
                var hash = System.Security.Cryptography.SHA256.HashData(bytes);
                var expectedHash = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");

                if (providedHash != expectedHash)
                {
                    _logger.LogWarning("State token hash mismatch");
                    return null;
                }

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating state token");
                return null;
            }
        }
    }
}

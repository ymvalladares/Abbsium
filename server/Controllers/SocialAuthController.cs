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
                $"&scope=email,public_profile,pages_show_list,pages_manage_posts,pages_read_engagement,business_management" +
                $"&auth_type=rerequest";

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

                if (pages.Count == 0)
                {
                    _logger.LogWarning("No pages found via /me/accounts, trying Business Manager API fallback");
                    pages = await GetFacebookPagesFromBusinessManager(tokenResponse.AccessToken);
                }

                if (pages.Count == 0)
                {
                    _logger.LogWarning("Facebook connection failed: no pages found for user {UserId}", userId);
                    return Redirect($"{callbackUrl}?status=error&provider=facebook&error=no_pages");
                }

                var pageId = pages[0].Id;
                var pageName = pages[0].Name;
                var pageToken = pages[0].AccessToken;

                var existingAccount = await _db.SocialAccounts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook");

                if (existingAccount != null)
                {
                    existingAccount.AccessToken = pageToken;
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
                        AccessToken = pageToken,
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

                if (data.GetArrayLength() == 0)
                {
                    _logger.LogWarning(
                        "Facebook /me/accounts returned empty data array. " +
                        "This usually means the page is managed through a Business Portfolio/Meta Business Suite " +
                        "rather than direct user ownership. The Business Manager fallback will be attempted. " +
                        "User token was granted at: {Timestamp}",
                        DateTime.UtcNow);
                }

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

        private async Task<List<(string Id, string Name, string AccessToken)>> GetFacebookPagesFromBusinessManager(string accessToken)
        {
            var pages = new List<(string Id, string Name, string AccessToken)>();

            try
            {
                // Step 1: Get businesses the user belongs to
                var client = _httpClientFactory.CreateClient();
                var businessesUrl = $"https://graph.facebook.com/v19.0/me/businesses?fields=id,name&access_token={accessToken}";

                _logger.LogInformation("Requesting businesses: {Url}", businessesUrl.Replace(accessToken, "***"));

                var businessesResponse = await client.GetAsync(businessesUrl);
                var businessesJson = await businessesResponse.Content.ReadAsStringAsync();

                _logger.LogInformation("Businesses response status: {StatusCode}", businessesResponse.StatusCode);
                _logger.LogInformation("Businesses response body: {Response}", businessesJson);

                if (!businessesResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get businesses: {Response}", businessesJson);
                    return pages;
                }

                using var businessesDoc = JsonDocument.Parse(businessesJson);
                var businessesRoot = businessesDoc.RootElement;

                if (!businessesRoot.TryGetProperty("data", out var businessesData) || businessesData.GetArrayLength() == 0)
                {
                    _logger.LogInformation("No businesses found for user");
                    return pages;
                }

                foreach (var business in businessesData.EnumerateArray())
                {
                    var businessId = business.TryGetProperty("id", out var bizIdElem) ? bizIdElem.GetString() : null;
                    if (string.IsNullOrEmpty(businessId)) continue;

                    _logger.LogInformation("Trying to get pages from business: {BusinessId}", businessId);

                    // Step 2: Get owned pages from the business
                    var ownedPagesUrl = $"https://graph.facebook.com/v19.0/{businessId}/owned_pages?fields=id,name,access_token&access_token={accessToken}";

                    _logger.LogInformation("Requesting owned pages: {Url}", ownedPagesUrl.Replace(accessToken, "***"));

                    var ownedPagesResponse = await client.GetAsync(ownedPagesUrl);
                    var ownedPagesJson = await ownedPagesResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("Owned pages response status: {StatusCode}", ownedPagesResponse.StatusCode);
                    _logger.LogInformation("Owned pages response body: {Response}", ownedPagesJson);

                    if (!ownedPagesResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to get owned pages from business {BusinessId}: {Response}", businessId, ownedPagesJson);
                        continue;
                    }

                    using var ownedPagesDoc = JsonDocument.Parse(ownedPagesJson);
                    var ownedPagesRoot = ownedPagesDoc.RootElement;

                    if (ownedPagesRoot.TryGetProperty("data", out var ownedPagesData))
                    {
                        foreach (var page in ownedPagesData.EnumerateArray())
                        {
                            var id = page.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                            var name = page.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                            var pageToken = page.TryGetProperty("access_token", out var tokenProp) ? tokenProp.GetString() : null;

                            _logger.LogInformation("Business page: id={Id}, name={Name}, hasToken={HasToken}", id, name, !string.IsNullOrEmpty(pageToken));

                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(pageToken))
                            {
                                pages.Add((id, name, pageToken));
                            }
                        }
                    }
                }

                _logger.LogInformation("Found {Count} Facebook pages from Business Manager", pages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pages from Business Manager");
            }

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
            var redirectUri = _config["Instagram:RedirectUri"];

            var url =
                "https://www.facebook.com/v19.0/dialog/oauth" +
                $"?client_id={_config["Facebook:ClientId"]}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={state}" +
                $"&scope=pages_show_list,pages_read_engagement,pages_manage_posts,instagram_basic,instagram_content_publish,business_management" +
                $"&response_type=code" +
                $"&auth_type=rerequest";

            _logger.LogInformation("Instagram connect initiated for user {UserId}, redirect_uri: {RedirectUri}", userId, redirectUri);
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
                _logger.LogWarning("Instagram callback error: {Error} - {Description}", error, errorDescription);
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
                // Step 1: Exchange code for Facebook user token
                var tokenResponse = await ExchangeFacebookCodeForInstagram(code);

                // Step 2: Get pages
                var pages = await GetFacebookPages(tokenResponse.AccessToken);

                if (pages.Count == 0)
                {
                    _logger.LogWarning("No pages found via /me/accounts for Instagram, trying Business Manager API fallback");
                    pages = await GetFacebookPagesFromBusinessManager(tokenResponse.AccessToken);
                }

                if (pages.Count == 0)
                {
                    _logger.LogWarning("Instagram connection failed: no Facebook pages found for user {UserId}", userId);
                    return Redirect($"{callbackUrl}?status=error&provider=instagram&error=no_pages");
                }

                // Step 3: Find page with Instagram Business Account
                string? pageId = null;
                string? pageName = null;
                string? pageToken = null;
                string? instagramAccountId = null;
                string? instagramUsername = null;

                foreach (var page in pages)
                {
                    var igAccountId = await GetInstagramBusinessAccountIdFromPage(page.AccessToken, page.Id);
                    if (!string.IsNullOrEmpty(igAccountId))
                    {
                        pageId = page.Id;
                        pageName = page.Name;
                        pageToken = page.AccessToken;
                        instagramAccountId = igAccountId;

                        // Get Instagram username
                        instagramUsername = await GetInstagramUsernameFromAccount(igAccountId, pageToken);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(instagramAccountId))
                {
                    _logger.LogWarning("Instagram connection failed: no Instagram Business Account found on any page for user {UserId}", userId);
                    return Redirect($"{callbackUrl}?status=error&provider=instagram&error=no_instagram_account");
                }

                _logger.LogInformation("Instagram Business Account found: {IgAccountId} on page {PageName} for user {UserId}", instagramAccountId, pageName, userId);

                var existingAccount = await _db.SocialAccounts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "instagram");

                if (existingAccount != null)
                {
                    existingAccount.AccessToken = pageToken;
                    existingAccount.RefreshToken = tokenResponse.LongLivedAccessToken ?? string.Empty;
                    existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingAccount.Scope = "pages_show_list,pages_read_engagement,pages_manage_posts,instagram_basic,instagram_content_publish";
                    existingAccount.IsActive = true;
                    existingAccount.ProviderAccountId = instagramAccountId;
                    existingAccount.AccountName = instagramUsername ?? pageName;
                    existingAccount.LastRefreshedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.SocialAccounts.Add(new SocialAccount
                    {
                        UserId = userId,
                        Provider = "instagram",
                        AccessToken = pageToken,
                        RefreshToken = tokenResponse.LongLivedAccessToken ?? string.Empty,
                        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                        Scope = "pages_show_list,pages_read_engagement,pages_manage_posts,instagram_basic,instagram_content_publish",
                        IsActive = true,
                        ProviderAccountId = instagramAccountId,
                        AccountName = instagramUsername ?? pageName,
                        LastRefreshedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Instagram account saved for user {UserId} with username {Username}", userId, instagramUsername);

                return Redirect($"{callbackUrl}?status=success&provider=instagram");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting Instagram account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=instagram&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? LongLivedAccessToken, int ExpiresIn)> ExchangeFacebookCodeForInstagram(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var redirectUri = _config["Instagram:RedirectUri"];

            _logger.LogInformation("Exchanging Facebook code for Instagram, redirect_uri: {RedirectUri}", redirectUri);

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
                _logger.LogError("Instagram token exchange failed: {StatusCode} - {Response}", response.StatusCode, responseString);
                throw new Exception($"Instagram token exchange failed: {responseString}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenElem))
            {
                var errorMsg = root.TryGetProperty("error", out var errorElem)
                    ? errorElem.GetRawText()
                    : responseString;
                _logger.LogError("Instagram token response missing access_token: {Response}", responseString);
                throw new Exception($"Instagram token exchange failed: {errorMsg}");
            }

            var accessToken = accessTokenElem.GetString() ?? throw new Exception("Missing access_token");
            var expiresIn = root.TryGetProperty("expires_in", out var expiresInElem) ? expiresInElem.GetInt32() : 5184000;
            var longLivedToken = root.TryGetProperty("long_lived_token", out var llToken) ? llToken.GetString() : null;

            _logger.LogInformation("Instagram token exchanged successfully, expires_in: {ExpiresIn}", expiresIn);
            return (accessToken, longLivedToken, expiresIn);
        }

        private async Task<string?> GetInstagramBusinessAccountIdFromPage(string pageAccessToken, string? pageId)
        {
            var client = _httpClientFactory.CreateClient();

            // Try with page ID first
            var url = string.IsNullOrEmpty(pageId)
                ? $"https://graph.facebook.com/v19.0/me?fields=instagram_business_account&access_token={pageAccessToken}"
                : $"https://graph.facebook.com/v19.0/{pageId}?fields=instagram_business_account&access_token={pageAccessToken}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to get Instagram Business Account from page: {StatusCode} - {Response}", response.StatusCode, errorContent);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("instagram_business_account", out var igAccount) &&
                igAccount.TryGetProperty("id", out var idElem))
            {
                var igId = idElem.GetString();
                _logger.LogInformation("Found Instagram Business Account: {IgAccountId}", igId);
                return igId;
            }

            _logger.LogInformation("No Instagram Business Account found on page");
            return null;
        }

        private async Task<string?> GetInstagramUsernameFromAccount(string instagramAccountId, string pageAccessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/{instagramAccountId}?fields=username&access_token={pageAccessToken}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return root.TryGetProperty("username", out var usernameElem)
                ? usernameElem.GetString()
                : null;
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
                $"&scope=user.info.basic,video.upload,video.publish" +
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
                var displayName = await GetTikTokDisplayName(tokenResponse.AccessToken);

                var existingAccount = await _db.SocialAccounts
                    .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "tiktok");

                if (existingAccount != null)
                {
                    existingAccount.AccessToken = tokenResponse.AccessToken;
                    existingAccount.RefreshToken = tokenResponse.RefreshToken ?? string.Empty;
                    existingAccount.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                    existingAccount.Scope = "user.info.basic,video.upload,video.publish";
                    existingAccount.IsActive = true;
                    existingAccount.ProviderAccountId = tokenResponse.OpenId;
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
                        ProviderAccountId = tokenResponse.OpenId,
                        AccountName = displayName,
                        LastRefreshedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("TikTok account saved for user {UserId} with display name {DisplayName}", userId, displayName);

                return Redirect($"{callbackUrl}?status=success&provider=tiktok");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting TikTok account for user {UserId}", userId);
                return Redirect($"{callbackUrl}?status=error&provider=tiktok&error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private async Task<(string AccessToken, string? RefreshToken, int ExpiresIn, string OpenId)> ExchangeTikTokCode(string code)
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

            var accessToken = root.TryGetProperty("access_token", out var atElem)
                ? atElem.GetString() ?? throw new Exception("Missing access_token")
                : throw new Exception($"TikTok token response missing access_token: {jsonString}");

            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400;
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
            var openId = root.TryGetProperty("open_id", out var openIdElem)
                ? openIdElem.GetString() ?? throw new Exception("Missing open_id")
                : throw new Exception($"TikTok token response missing open_id: {jsonString}");

            return (accessToken, refreshToken, expiresIn, openId);
        }

        private async Task<string> GetTikTokDisplayName(string accessToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name,avatar_url";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("TikTok user info request failed: {StatusCode}", response.StatusCode);
                    return "TikTok Account";
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("user", out var user) &&
                        user.TryGetProperty("display_name", out var displayNameElem))
                    {
                        return displayNameElem.GetString() ?? "TikTok Account";
                    }

                    if (data.TryGetProperty("display_name", out var displayNameElem2))
                    {
                        return displayNameElem2.GetString() ?? "TikTok Account";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting TikTok display name");
            }

            return "TikTok Account";
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

            // Revoke Facebook/Instagram permissions before deleting
            if (provider.ToLower() == "facebook" || provider.ToLower() == "instagram")
            {
                try
                {
                    await RevokeFacebookPermissions(acc.AccessToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to revoke {Provider} permissions, continuing with deletion", provider);
                }
            }

            _db.SocialAccounts.Remove(acc);
            await _db.SaveChangesAsync();

            _logger.LogInformation("{Provider} account disconnected and deleted for user {UserId}", provider, userId);
            return Ok(new { message = $"{provider} disconnected successfully" });
        }

        // ================= REVOKE PERMISSIONS =================
        [Authorize]
        [HttpPost("revoke/{provider}")]
        public async Task<IActionResult> RevokePermissions(string provider)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider.ToLower() == provider.ToLower());

            if (acc == null)
                return NotFound(new { error = $"{provider} account not found" });

            try
            {
                if (provider.ToLower() == "facebook" || provider.ToLower() == "instagram")
                {
                    await RevokeFacebookPermissions(acc.AccessToken);
                }
                else
                {
                    return BadRequest(new { error = $"Permission revocation not supported for {provider}" });
                }

                _logger.LogInformation("{Provider} permissions revoked for user {UserId}", provider, userId);
                return Ok(new { message = $"{provider} permissions revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking {Provider} permissions for user {UserId}", provider, userId);
                return StatusCode(500, new { error = "Failed to revoke permissions", details = ex.Message });
            }
        }

        private async Task RevokeFacebookPermissions(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/me/permissions?access_token={accessToken}";

            var response = await client.DeleteAsync(url);
            var responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Revoke permissions response: {StatusCode} - {Response}", response.StatusCode, responseString);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to revoke permissions: {responseString}");
            }
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
                    acc.LastRefreshedAt = DateTime.UtcNow;
                }
                else if (provider.ToLower() == "instagram")
                {
                    var newToken = await RefreshInstagramToken(acc.AccessToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                    acc.LastRefreshedAt = DateTime.UtcNow;
                }
                else if (provider.ToLower() == "youtube" && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshYouTubeToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                    acc.LastRefreshedAt = DateTime.UtcNow;
                }
                else if (provider.ToLower() == "tiktok" && !string.IsNullOrEmpty(acc.RefreshToken))
                {
                    var newToken = await RefreshTikTokToken(acc.RefreshToken);
                    acc.AccessToken = newToken.AccessToken;
                    acc.RefreshToken = newToken.RefreshToken ?? acc.RefreshToken;
                    acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                    acc.LastRefreshedAt = DateTime.UtcNow;
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

        private async Task<(string AccessToken, int ExpiresIn)> RefreshInstagramToken(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/oauth/access_token" +
                      $"?grant_type=fb_exchange_token" +
                      $"&client_id={_config["Facebook:ClientId"]}" +
                      $"&client_secret={_config["Facebook:ClientSecret"]}" +
                      $"&fb_exchange_token={accessToken}";

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Instagram token refresh failed: {StatusCode} - {Response}", response.StatusCode, json);
                throw new Exception($"Instagram token refresh failed: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var atElem))
            {
                _logger.LogError("Instagram token refresh response missing access_token: {Response}", json);
                throw new Exception($"Instagram token refresh failed: {json}");
            }

            return (
                atElem.GetString() ?? throw new Exception("Missing access_token"),
                root.TryGetProperty("expires_in", out var expElem) ? expElem.GetInt32() : 5184000
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
                    if (!isValid)
                    {
                        try
                        {
                            var newToken = await RefreshInstagramToken(acc.AccessToken);
                            acc.AccessToken = newToken.AccessToken;
                            acc.ExpiresAt = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn);
                            acc.LastRefreshedAt = DateTime.UtcNow;
                            isValid = true;
                            wasRefreshed = true;
                        }
                        catch (Exception refreshEx)
                        {
                            _logger.LogWarning(refreshEx, "Instagram token refresh failed for user {UserId}", userId);
                        }
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
                            acc.LastRefreshedAt = DateTime.UtcNow;
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
                            acc.LastRefreshedAt = DateTime.UtcNow;
                            isValid = true;
                            wasRefreshed = true;
                        }
                        catch { }
                    }
                }

                if (!isValid)
                {
                    acc.IsActive = false;
                    _logger.LogWarning("{Provider} token invalid for user {UserId}, account marked as inactive", acc.Provider, userId);
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
                    $"https://graph.facebook.com/v19.0/me?access_token={accessToken}&fields=id,name");
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
            public string VideoUrl { get; set; }
        }

        [Authorize]
        [HttpPost("facebook/post")]
        public async Task<IActionResult> PostToFacebook([FromBody] FacebookPostRequest request)
        {
            return await PublishToFacebook(request);
        }

        [Authorize]
        [HttpPost("facebook/publish")]
        public async Task<IActionResult> PublishToFacebook([FromBody] FacebookPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            if (string.IsNullOrEmpty(request.Message) && string.IsNullOrEmpty(request.PhotoUrl) && string.IsNullOrEmpty(request.VideoUrl))
                return BadRequest("Message, PhotoUrl or VideoUrl is required");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;

                if (!string.IsNullOrEmpty(request.VideoUrl))
                {
                    return await PostVideo(client, acc, request, pageId);
                }

                if (!string.IsNullOrEmpty(request.PhotoUrl))
                {
                    return await PostPhoto(client, acc, request, pageId);
                }

                return await PostText(client, acc, request, pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to Facebook for user {UserId}", userId);
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
                return await PostPhoto(client, acc, request, pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting photo to Facebook for user {UserId}", userId);
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("facebook/video")]
        public async Task<IActionResult> PostVideoToFacebook([FromBody] FacebookPostRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var acc = await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == "facebook" && x.IsActive);

            if (acc == null)
                return BadRequest("Facebook not connected");

            if (string.IsNullOrEmpty(request.VideoUrl))
                return BadRequest("VideoUrl is required");

            try
            {
                var client = _httpClientFactory.CreateClient();
                var pageId = request.PageId ?? acc.ProviderAccountId;
                return await PostVideo(client, acc, request, pageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting video to Facebook for user {UserId}", userId);
                return StatusCode(500, new { error = "Server error", details = ex.Message });
            }
        }

        private async Task<IActionResult> PostText(HttpClient client, SocialAccount acc, FacebookPostRequest request, string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
                return BadRequest("Facebook Page ID is required. Connect a Facebook Page first.");

            var fbUrl = $"https://graph.facebook.com/{pageId}/feed";

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

        private async Task<IActionResult> PostPhoto(HttpClient client, SocialAccount acc, FacebookPostRequest request, string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
                return BadRequest("Facebook Page ID is required. Connect a Facebook Page first.");

            var fbUrl = $"https://graph.facebook.com/{pageId}/photos";

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

        private async Task<IActionResult> PostVideo(HttpClient client, SocialAccount acc, FacebookPostRequest request, string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
                return BadRequest("Facebook Page ID is required. Connect a Facebook Page first.");

            var fbUrl = $"https://graph.facebook.com/{pageId}/videos";

            var content = new MultipartFormDataContent();

            byte[] bytes;
            if (request.VideoUrl.StartsWith("data:"))
            {
                var base64Data = request.VideoUrl.Substring(request.VideoUrl.IndexOf(",") + 1);
                bytes = Convert.FromBase64String(base64Data);
            }
            else if (request.VideoUrl.StartsWith("http"))
            {
                var videoResponse = await client.GetAsync(request.VideoUrl);
                videoResponse.EnsureSuccessStatusCode();
                bytes = await videoResponse.Content.ReadAsByteArrayAsync();
            }
            else
            {
                return BadRequest("Invalid VideoUrl format");
            }

            content.Add(new ByteArrayContent(bytes), "source", "video.mp4");
            content.Add(new StringContent(request.Message ?? ""), "description");
            content.Add(new StringContent(acc.AccessToken), "access_token");

            var fbResponse = await client.PostAsync(fbUrl, content);
            var responseString = await fbResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Facebook video post response: {StatusCode} - {Response}", fbResponse.StatusCode, responseString);

            if (!fbResponse.IsSuccessStatusCode)
            {
                return BadRequest(new { error = "Facebook API error", details = responseString });
            }

            return Ok(new { status = "Video published", response = responseString, pageId = pageId });
        }

        // ================= HELPERS =================
        private string GenerateStateToken(string userId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var plainText = $"{userId}|{timestamp}";
            var key = _config["JwtSettings:Key"] ?? "fallback-key-for-state";
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
            var key = _config["JwtSettings:Key"] ?? "fallback-key-for-state";
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

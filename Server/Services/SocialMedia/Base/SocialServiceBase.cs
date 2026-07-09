using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Server.Data;
using Server.Entitys;
using Server.Models.SocialMedia.Enums;

namespace Server.Services.SocialMedia.Base
{
    public abstract class SocialServiceBase
    {
        protected readonly DbContext_app _db;
        protected readonly IConfiguration _config;
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly ILogger _logger;

        protected SocialServiceBase(
            DbContext_app db,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ILogger logger)
        {
            _db = db;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected abstract string ProviderName { get; }

        protected async Task<SocialAccount?> GetSocialAccountAsync(string userId)
        {
            return await _db.SocialAccounts
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == ProviderName && x.IsActive);
        }

        protected string GenerateStateToken(string userId)
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

        protected string? ValidateStateToken(string state)
        {
            try
            {
                var padded = state.PadRight(state.Length + (4 - state.Length % 4) % 4, '=');
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace("-", "+").Replace("_", "/")));

                var parts = decoded.Split('|');
                if (parts.Length != 3) return null;

                var userId = parts[0];
                var timestamp = long.Parse(parts[1]);
                var providedHash = parts[2];

                var maxAge = TimeSpan.FromMinutes(15);
                var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                var age = DateTimeOffset.UtcNow - tokenTime;

                if (age > maxAge) return null;

                var plainText = $"{userId}|{timestamp}";
                var key = _config["JwtSettings:Key"] ?? "fallback-key-for-state";
                var bytes = System.Text.Encoding.UTF8.GetBytes(plainText + key);
                var hash = System.Security.Cryptography.SHA256.HashData(bytes);
                var expectedHash = Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").Replace("=", "");

                return providedHash == expectedHash ? userId : null;
            }
            catch
            {
                return null;
            }
        }

        protected string GetClientCallbackUrl()
        {
            var clientUrl = _config["ClientUrl"] ?? "http://localhost:3000";
            return $"{clientUrl}/auth/social-callback";
        }

        protected async Task<byte[]> DownloadMediaAsync(string url)
        {
            var client = _httpClientFactory.CreateClient("SocialMedia");
            _logger.LogInformation("Downloading media from: {Url}", url);
            
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to download media: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new Exception($"Failed to download media: {response.StatusCode}");
            }
            
            var bytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Media downloaded successfully: {Size} bytes", bytes.Length);
            return bytes;
        }

        protected byte[] ExtractBase64Media(string dataUri)
        {
            var base64Data = dataUri.Substring(dataUri.IndexOf(",") + 1);
            return Convert.FromBase64String(base64Data);
        }
    }
}

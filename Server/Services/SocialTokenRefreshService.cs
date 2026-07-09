using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Server.Data;

namespace Server.Services
{
    public class SocialTokenRefreshService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<SocialTokenRefreshService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public SocialTokenRefreshService(
            IServiceProvider services,
            ILogger<SocialTokenRefreshService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration config)
        {
            _services = services;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SocialTokenRefreshService started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DbContext_app>();

                    var accounts = await db.SocialAccounts
                        .Where(x => x.IsActive && !string.IsNullOrEmpty(x.RefreshToken))
                        .ToListAsync(stoppingToken);

                    foreach (var acc in accounts)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        // Refresh if token expires within 30 minutes or was never refreshed
                        bool needsRefresh = acc.ExpiresAt <= DateTime.UtcNow.AddMinutes(30)
                            || acc.LastRefreshedAt == null
                            || (acc.LastRefreshedAt.Value.AddDays(3) <= DateTime.UtcNow);

                        if (!needsRefresh) continue;

                        try
                        {
                            bool refreshed = false;

                            if (acc.Provider == "youtube")
                            {
                                refreshed = await RefreshYouTubeToken(db, acc, stoppingToken);
                            }
                            else if (acc.Provider == "instagram")
                            {
                                refreshed = await RefreshInstagramToken(db, acc, stoppingToken);
                            }
                            else if (acc.Provider == "tiktok")
                            {
                                refreshed = await RefreshTikTokToken(db, acc, stoppingToken);
                            }
                            else if (acc.Provider == "facebook")
                            {
                                refreshed = await RefreshFacebookToken(db, acc, stoppingToken);
                            }

                            if (refreshed)
                            {
                                acc.LastRefreshedAt = DateTime.UtcNow;
                                await db.SaveChangesAsync(stoppingToken);
                                _logger.LogInformation("Refreshed {Provider} token for user {UserId}", acc.Provider, acc.UserId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to refresh {Provider} token for user {UserId}", acc.Provider, acc.UserId);

                            // Don't delete - just mark inactive if refresh consistently fails
                            // The user will see it as disconnected and can reconnect
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SocialTokenRefreshService loop");
                }

                // Check every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }

            _logger.LogInformation("SocialTokenRefreshService stopped");
        }

        private async Task<bool> RefreshYouTubeToken(DbContext_app db, Entitys.SocialAccount acc, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://oauth2.googleapis.com/token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _config["YouTube:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["YouTube:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", acc.RefreshToken)
            });

            var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var atElem))
            {
                acc.AccessToken = atElem.GetString()!;
                acc.ExpiresAt = DateTime.UtcNow.AddSeconds(
                    root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600
                );
                if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is string newRt)
                {
                    acc.RefreshToken = newRt;
                }
                return true;
            }

            return false;
        }

        private async Task<bool> RefreshInstagramToken(DbContext_app db, Entitys.SocialAccount acc, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://graph.instagram.com/refresh_access_token";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "ig_refresh_token"),
                new KeyValuePair<string, string>("client_id", _config["Instagram:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["Instagram:ClientSecret"]),
                new KeyValuePair<string, string>("refresh_token", acc.RefreshToken)
            });

            var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var atElem))
            {
                acc.AccessToken = atElem.GetString()!;
                acc.ExpiresAt = DateTime.UtcNow.AddSeconds(
                    root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000
                );
                if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is string newRt)
                {
                    acc.RefreshToken = newRt;
                }
                return true;
            }

            return false;
        }

        private async Task<bool> RefreshTikTokToken(DbContext_app db, Entitys.SocialAccount acc, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = "https://open.tiktokapis.com/v2/oauth/token/";
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_key", _config["TikTok:ClientId"]),
                new KeyValuePair<string, string>("client_secret", _config["TikTok:ClientSecret"]),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", acc.RefreshToken)
            });

            var response = await client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var atElem))
            {
                acc.AccessToken = atElem.GetString()!;
                acc.ExpiresAt = DateTime.UtcNow.AddSeconds(
                    root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 86400
                );
                if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is string newRt)
                {
                    acc.RefreshToken = newRt;
                }
                return true;
            }

            return false;
        }

        private async Task<bool> RefreshFacebookToken(DbContext_app db, Entitys.SocialAccount acc, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v19.0/oauth/access_token" +
                      $"?grant_type=fb_exchange_token" +
                      $"&client_id={_config["Facebook:ClientId"]}" +
                      $"&client_secret={_config["Facebook:ClientSecret"]}" +
                      $"&fb_exchange_token={acc.AccessToken}";

            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var atElem))
            {
                acc.AccessToken = atElem.GetString()!;
                acc.ExpiresAt = DateTime.UtcNow.AddSeconds(
                    root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 5184000
                );
                return true;
            }

            return false;
        }
    }
}

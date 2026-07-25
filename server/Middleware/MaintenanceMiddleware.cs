using Microsoft.AspNetCore.Identity;
using Server.Data;
using Server.Entitys;
using System.Net;
using System.Text.Json;

namespace Server.Middleware
{
    public class MaintenanceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _env;

        private static bool _cachedMaintenanceMode = false;
        private static DateTime _lastCheck = DateTime.MinValue;
        private static readonly object _lock = new object();

        public MaintenanceMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, IHostEnvironment env)
        {
            _next = next;
            _scopeFactory = scopeFactory;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_env.IsDevelopment())
            {
                await _next(context);
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastCheck).TotalSeconds > 30)
            {
                lock (_lock)
                {
                    if ((now - _lastCheck).TotalSeconds > 30)
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<DbContext_app>();
                            var settings = dbContext.SiteSettings.FirstOrDefault();
                            _cachedMaintenanceMode = settings?.IsUnderMaintenance ?? false;
                        }
                        catch
                        {
                        }
                        _lastCheck = now;
                    }
                }
            }

            if (_cachedMaintenanceMode)
            {
                var path = context.Request.Path.Value?.ToLowerInvariant();
                var isApi = path?.StartsWith("/api/") == true;
                var isStatic = path?.StartsWith("/static/") == true || path?.EndsWith(".js") == true || path?.EndsWith(".css") == true;

                if (isStatic)
                {
                    await _next(context);
                    return;
                }

                var userManager = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<UserManager<User_data>>();
                var user = context.User;

                if (user.Identity?.IsAuthenticated == true)
                {
                    var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var appUser = await userManager.FindByIdAsync(userId);
                        if (appUser != null && await userManager.IsInRoleAsync(appUser, "Admin"))
                        {
                            await _next(context);
                            return;
                        }
                    }
                }

                if (isApi)
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;

                    var response = new
                    {
                        statusCode = 503,
                        message = "The site is currently under maintenance. Please try again later.",
                        isUnderMaintenance = true
                    };

                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var json = JsonSerializer.Serialize(response, options);
                    await context.Response.WriteAsync(json);
                    return;
                }

                await _next(context);
                return;
            }

            await _next(context);
        }
    }
}

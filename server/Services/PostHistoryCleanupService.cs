using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services
{
    public class PostHistoryCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PostHistoryCleanupService> _logger;

        public PostHistoryCleanupService(
            IServiceProvider serviceProvider,
            ILogger<PostHistoryCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DbContext_app>();

                    var cutoff = DateTime.UtcNow.AddDays(-30);
                    var oldRecords = await db.PostHistory
                        .Where(x => x.PublishedAt < cutoff)
                        .ToListAsync(stoppingToken);

                    if (oldRecords.Count > 0)
                    {
                        db.PostHistory.RemoveRange(oldRecords);
                        var deleted = await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Auto-cleaned {Deleted} post history records older than 30 days", deleted);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during post history cleanup");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}

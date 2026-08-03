using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Server.Services
{
    public class PublishWorkerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBackgroundPublishQueue _queue;
        private readonly ILogger<PublishWorkerService> _logger;

        public PublishWorkerService(
            IServiceProvider serviceProvider,
            IBackgroundPublishQueue queue,
            ILogger<PublishWorkerService> logger)
        {
            _serviceProvider = serviceProvider;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _queue.DequeueAsync(stoppingToken);
                    using var scope = _serviceProvider.CreateScope();
                    await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing background publish work item");
                }
            }
        }
    }
}

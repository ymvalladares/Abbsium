using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Server.Chat.HubFolder
{
    [Authorize]
    public class PublishingHub : Hub
    {
        private readonly ILogger<PublishingHub> _logger;

        public PublishingHub(ILogger<PublishingHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"publish_{userId}");
            
            _logger.LogInformation("User {UserId} connected to PublishingHub", userId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"publish_{userId}");
                _logger.LogInformation("User {UserId} disconnected from PublishingHub", userId);
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}

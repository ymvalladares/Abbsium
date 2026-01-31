using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Server.Repositories.IRepositories;
using System.Security.Claims;

namespace Server.Chat.HubFolder
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                Context.Abort();
                return;
            }

            if (userRole == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Admin_{userId}");
                _logger.LogInformation($"✅ Admin {userId} connected");
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation($"✅ User {userId} connected");
            }

            await base.OnConnectedAsync();
        }

        // Usuario envía mensaje a Admin específico
        public async Task SendMessageToAdmin(string adminId, string content)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "User";

            if (string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("Error", "Invalid message");
                return;
            }

            try
            {
                var message = await _chatService.SendUserMessageAsync(userId, adminId, content);

                // Confirmar al usuario
                await Clients.Caller.SendAsync("messageSent", new
                {
                    id = message.Id,
                    content = message.Content,
                    sentAt = message.SentAt,
                    isAdminMessage = false
                });

                // Notificar al Admin específico
                await Clients.Group($"Admin_{adminId}").SendAsync("newUserMessage", new
                {
                    conversationId = message.ConversationId,
                    messageId = message.Id,
                    userId,
                    userName,
                    content = message.Content,
                    sentAt = message.SentAt
                });

                _logger.LogInformation($"User {userId} → Admin {adminId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending user message");
                await Clients.Caller.SendAsync("Error", "Failed to send");
            }
        }

        // Admin responde
        public async Task SendAdminReply(string conversationId, string content)
        {
            var adminId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var adminName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";

            if (!Guid.TryParse(conversationId, out var convId) || string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("Error", "Invalid params");
                return;
            }

            try
            {
                var (message, targetUserId) = await _chatService.SendAdminReplyAsync(convId, adminId, content);

                await Clients.Caller.SendAsync("adminReplySent", new
                {
                    id = message.Id,
                    content = message.Content,
                    sentAt = message.SentAt,
                    isAdminMessage = true
                });

                await Clients.Group($"User_{targetUserId}").SendAsync("newAdminMessage", new
                {
                    messageId = message.Id,
                    adminName,
                    content = message.Content,
                    sentAt = message.SentAt
                });

                _logger.LogInformation($"Admin {adminId} → User {targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin reply");
                await Clients.Caller.SendAsync("Error", "Failed to send");
            }
        }

        public async Task MarkAsRead(string conversationId)
        {
            var userId = Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(conversationId, out var convId)) return;

            try
            {
                await _chatService.MarkMessagesAsReadAsync(convId, userId);
                await Clients.Caller.SendAsync("messagesMarkedAsRead", conversationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking as read: {conversationId}");
            }
        }
    }
}
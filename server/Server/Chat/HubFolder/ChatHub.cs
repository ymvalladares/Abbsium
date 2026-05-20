using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Server.Entitys;
using Server.Repositories.IRepositories;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace Server.Chat.HubFolder
{
    //[Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly UserManager<User_data> _userManager;
        private readonly ILogger<ChatHub> _logger;

        // ⭐ DICCIONARIO ESTÁTICO para rastrear usuarios conectados
        private class OnlineUserInfo
        {
            public HashSet<string> ConnectionIds { get; set; } = new();
            public string UserName { get; set; } = "";
        }
        private static readonly ConcurrentDictionary<string, OnlineUserInfo> OnlineUsers = new();

        public ChatHub(IChatService chatService, UserManager<User_data> userManager, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _userManager = userManager;
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

            // ⭐ REGISTRAR usuario como conectado
            var userName = Context.User.FindFirst(ClaimTypes.Name)?.Value ?? "";
            // Fallback: buscar nombre desde la BD si el claim no está en el token
            if (string.IsNullOrEmpty(userName))
            {
                var user = await _userManager.FindByIdAsync(userId);
                userName = user?.UserName ?? "";
            }
            var isFirstConnection = false;

            OnlineUsers.AddOrUpdate(
                userId,
                _ => new OnlineUserInfo
                {
                    ConnectionIds = new HashSet<string> { Context.ConnectionId },
                    UserName = userName
                },
                (_, existing) =>
                {
                    if (existing.ConnectionIds.Count == 0) isFirstConnection = true;
                    existing.ConnectionIds.Add(Context.ConnectionId);
                    existing.UserName = userName;
                    return existing;
                }
            );

            if (isFirstConnection || OnlineUsers[userId].ConnectionIds.Count == 1)
            {
                await Clients.All.SendAsync("userStatusChanged", new
                {
                    userId = userId,
                    userName = userName,
                    isOnline = true
                });
            }

            // ⭐ AGREGAR A GRUPOS - MUY IMPORTANTE
            if (userRole == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Admin_{userId}");
                _logger.LogInformation($"✅ Admin {userId} joined group: Admin_{userId}");
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
                _logger.LogInformation($"✅ User {userId} joined group: User_{userId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                // ⭐ ELIMINAR esta conexión específica
                if (OnlineUsers.TryGetValue(userId, out var info))
                {
                    info.ConnectionIds.Remove(Context.ConnectionId);

                    // Si ya no tiene conexiones, está offline
                    if (info.ConnectionIds.Count == 0)
                    {
                        var disconnectedUserName = info.UserName;
                        OnlineUsers.TryRemove(userId, out _);

                        _logger.LogInformation($"❌ User {userId} is NOW OFFLINE");

                        // ⭐ NOTIFICAR A TODOS que este usuario está offline
                        await Clients.All.SendAsync("userStatusChanged", new
                        {
                            userId = userId,
                            userName = disconnectedUserName,
                            isOnline = false
                        });
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ⭐ Endpoint para obtener usuarios online actuales con nombres
        public async Task GetOnlineUsers()
        {
            var onlineUsers = OnlineUsers
                .Where(kvp => kvp.Value.ConnectionIds.Count > 0)
                .Select(kvp => new
                {
                    userId = kvp.Key,
                    userName = kvp.Value.UserName
                })
                .ToList();
            await Clients.Caller.SendAsync("onlineUsersList", onlineUsers);
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

                // ⭐ CONFIRMAR al usuario - MISMO NOMBRE que en frontend
                await Clients.Caller.SendAsync("messageSent", new
                {
                    id = message.Id,
                    content = message.Content,
                    sentAt = message.SentAt,
                    isAdminMessage = false
                });

                _logger.LogInformation($"✅ Sent 'messageSent' to User {userId}");

                // ⭐ NOTIFICAR al Admin - MISMO NOMBRE que en frontend
                await Clients.Group($"Admin_{adminId}").SendAsync("newUserMessage", new
                {
                    conversationId = message.ConversationId,
                    messageId = message.Id,
                    userId,
                    userName,
                    content = message.Content,
                    sentAt = message.SentAt
                });

                _logger.LogInformation($"✅ Sent 'newUserMessage' to Admin_{adminId}");
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

                // ⭐ CONFIRMAR al admin - MISMO NOMBRE que en frontend
                await Clients.Caller.SendAsync("adminReplySent", new
                {
                    id = message.Id,
                    content = message.Content,
                    sentAt = message.SentAt,
                    isAdminMessage = true
                });

                _logger.LogInformation($"✅ Sent 'adminReplySent' to Admin {adminId}");

                // ⭐ NOTIFICAR al usuario - MISMO NOMBRE que en frontend
                await Clients.Group($"User_{targetUserId}").SendAsync("newAdminMessage", new
                {
                    messageId = message.Id,
                    adminName,
                    content = message.Content,
                    sentAt = message.SentAt
                });

                _logger.LogInformation($"✅ Sent 'newAdminMessage' to User_{targetUserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending admin reply");
                await Clients.Caller.SendAsync("Error", "Failed to send");
            }
        }

        public async Task MarkAsRead(string conversationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(conversationId, out var convId)) return;

            try
            {
                var (readMessageIds, otherUserId) = await _chatService.MarkMessagesAsReadAsync(convId, userId);
                await Clients.Caller.SendAsync("messagesMarkedAsRead", conversationId);

                // Notificar al remitente que sus mensajes fueron leídos
                if (readMessageIds.Count > 0 && !string.IsNullOrEmpty(otherUserId))
                {
                    var targetGroup = userRole == "Admin" ? $"User_{otherUserId}" : $"Admin_{otherUserId}";
                    await Clients.Group(targetGroup).SendAsync("messagesRead", new
                    {
                        conversationId,
                        messageIds = readMessageIds,
                        readBy = userId
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking as read: {conversationId}");
            }
        }
    }
}
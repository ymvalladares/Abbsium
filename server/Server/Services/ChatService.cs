using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Chat.Dto;
using Server.Chat.Entitys;
using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Services
{
    public class ChatService : IChatService
    {
        private readonly DbContext_app _context;
        private readonly ILogger<ChatService> _logger;
        private readonly UserManager<User_data> _userManager;

        public ChatService(DbContext_app context, ILogger<ChatService> logger, UserManager<User_data> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // Usuario envía mensaje a un Admin específico
        public async Task<Message> SendUserMessageAsync(string userId, string adminId, string content)
        {
            var conversation = await GetOrCreateConversationAsync(userId, adminId);

            var message = new Message
            {
                ConversationId = conversation.Id,
                SenderId = userId,
                Content = content.Trim(),
                IsAdminMessage = false
            };

            _context.Messages.Add(message);
            conversation.LastMessageAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {userId} sent message to Admin {adminId}");
            return message;
        }

        // Admin responde a un usuario
        public async Task<(Message message, string targetUserId)> SendAdminReplyAsync(
            Guid conversationId, string adminId, string content)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.AdminId == adminId);

            if (conversation == null)
                throw new KeyNotFoundException("Conversation not found");

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = adminId,
                Content = content.Trim(),
                IsAdminMessage = true
            };

            _context.Messages.Add(message);
            conversation.LastMessageAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Admin {adminId} replied to User {conversation.UserId}");
            return (message, conversation.UserId);
        }

        // Obtener o crear conversación entre User y Admin
        public async Task<Conversation> GetOrCreateConversationAsync(string userId, string adminId)
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.UserId == userId && c.AdminId == adminId && c.IsActive);

            if (conversation == null)
            {
                conversation = new Conversation
                {
                    UserId = userId,
                    AdminId = adminId
                };
                _context.Conversations.Add(conversation);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Created conversation: User {userId} <-> Admin {adminId}");
            }

            return conversation;
        }

        // Obtener mensajes de una conversación
        public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(
            Guid conversationId, string requestingUserId)
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .Include(c => c.User)
                .Include(c => c.Admin)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
                throw new KeyNotFoundException("Conversation not found");

            // Verificar permisos
            var isAdmin = await IsUserAdminAsync(requestingUserId);
            if (conversation.UserId != requestingUserId &&
                conversation.AdminId != requestingUserId &&
                !isAdmin)
                throw new UnauthorizedAccessException("Access denied");

            return conversation.Messages
                .OrderBy(m => m.SentAt)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsAdminMessage = m.IsAdminMessage,
                    IsRead = m.IsRead,
                    SenderName = m.IsAdminMessage ? conversation.Admin.UserName : conversation.User.UserName
                })
                .ToList();
        }

        // Obtener lista de Admins (para usuarios normales)
        public async Task<IEnumerable<AdminDto>> GetAdminsAsync()
        {
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole == null) return new List<AdminDto>();

            var adminUserIds = await _context.UserRoles
                .Where(ur => ur.RoleId == adminRole.Id)
                .Select(ur => ur.UserId)
                .ToListAsync();

            var admins = await _context.Users
                .Where(u => adminUserIds.Contains(u.Id))
                .Select(u => new AdminDto
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email
                })
                .ToListAsync();

            return admins;
        }

        // Obtener conversaciones del Admin (solo las que tienen mensajes)
        public async Task<IEnumerable<ConversationDto>> GetAdminConversationsAsync(string adminId)
        {
            var conversations = await _context.Conversations
                .Include(c => c.Messages)
                .Include(c => c.User)
                .Where(c => c.AdminId == adminId && c.IsActive)
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .ToListAsync();

            return conversations.Select(c => new ConversationDto
            {
                Id = c.Id,
                UserId = c.UserId,
                UserName = c.User.UserName ?? "User",
                UserEmail = c.User.Email ?? "",
                LastMessageAt = c.LastMessageAt ?? c.CreatedAt,
                UnreadCount = c.Messages.Count(m => !m.IsRead && !m.IsAdminMessage),
                LastMessage = c.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? ""
            }).ToList();
        }

        // Obtener conversaciones del Usuario (con cada Admin)
        public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId)
        {
            var conversations = await _context.Conversations
                .Include(c => c.Messages)
                .Include(c => c.Admin)
                .Where(c => c.UserId == userId && c.IsActive)
                .ToListAsync();

            return conversations.Select(c => new ConversationDto
            {
                Id = c.Id,
                UserId = c.AdminId,
                UserName = c.Admin.UserName ?? "Admin",
                UserEmail = c.Admin.Email ?? "",
                LastMessageAt = c.LastMessageAt ?? c.CreatedAt,
                UnreadCount = c.Messages.Count(m => !m.IsRead && m.IsAdminMessage),
                LastMessage = c.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? ""
            }).ToList();
        }

        public async Task MarkMessagesAsReadAsync(Guid conversationId, string userId)
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null) return;

            var isAdmin = await IsUserAdminAsync(userId);

            var messagesToMark = conversation.Messages
                .Where(m => !m.IsRead &&
                       ((isAdmin && !m.IsAdminMessage) ||
                        (!isAdmin && m.IsAdminMessage)))
                .ToList();

            foreach (var message in messagesToMark)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            if (messagesToMark.Any())
                await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            var conversations = await _context.Conversations
                .Include(c => c.Messages)
                .Where(c => c.UserId == userId && c.IsActive)
                .ToListAsync();

            return conversations.Sum(c =>
                c.Messages.Count(m => !m.IsRead && m.IsAdminMessage));
        }

        private async Task<bool> IsUserAdminAsync(string userId)
        {
            return await _context.UserRoles
                .AnyAsync(ur => ur.UserId == userId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"));
        }
    }
}
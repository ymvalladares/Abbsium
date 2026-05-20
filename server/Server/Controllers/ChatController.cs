using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Chat.Dto;
using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;
using System.Security.Claims;

namespace Server.Controllers
{
    [Authorize]
    public class ChatController : Base_Control_Api
    {
        private readonly IChatService _chatService;
        private readonly DbContext_app _context;
        private readonly UserManager<User_data> _userManager;

        public ChatController(IChatService chatService, DbContext_app context, UserManager<User_data> userManager)
        {
            _chatService = chatService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("admins")]
        public async Task<ActionResult> GetAdmins()
        {
            var admins = await _chatService.GetAdminsAsync();
            return Ok(admins);
        }

        [HttpGet("my-conversations")]
        public async Task<ActionResult> GetMyConversations()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (role == "Admin")
            {
                var convs = await _chatService.GetAdminConversationsAsync(userId);
                return Ok(convs);
            }
            else
            {
                var convs = await _chatService.GetUserConversationsAsync(userId);
                return Ok(convs);
            }
        }

        [HttpGet("conversations/{conversationId}/messages")]
        public async Task<ActionResult> GetMessages(Guid conversationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                var messages = await _chatService.GetConversationMessagesAsync(conversationId, userId);
                return Ok(messages);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult> GetUnreadCount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var count = await _chatService.GetUnreadCountAsync(userId);
            return Ok(new { unreadCount = count });
        }

        [HttpPost("conversations/{conversationId}/mark-read")]
        public async Task<ActionResult> MarkAsRead(Guid conversationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await _chatService.MarkMessagesAsReadAsync(conversationId, userId);
            return Ok();
        }

        [HttpPost("migrate-self-conversation")]
        public async Task<ActionResult> MigrateSelfConversation([FromBody] MigrateSelfConversationRequest request)
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (role != "Admin")
                return Forbid();

            var realUser = await _userManager.FindByEmailAsync(request.UserEmail);
            if (realUser == null)
                return NotFound(new { message = "User not found with that email" });

            var selfConversations = await _context.Conversations
                .Include(c => c.Messages)
                .Where(c => c.UserId == adminId && c.AdminId == adminId && c.IsActive)
                .ToListAsync();

            if (selfConversations.Count == 0)
                return Ok(new { message = "No self-conversations found to migrate" });

            foreach (var conv in selfConversations)
            {
                conv.UserId = realUser.Id;

                foreach (var msg in conv.Messages)
                {
                    if (!msg.IsAdminMessage)
                        msg.SenderId = realUser.Id;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Migrated {selfConversations.Count} conversation(s) to user {realUser.UserName}" });
        }
    }

    public class MigrateSelfConversationRequest
    {
        public string UserEmail { get; set; }
    }
}
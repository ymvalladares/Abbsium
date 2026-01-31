using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Chat.Dto;
using Server.Repositories.IRepositories;
using System.Security.Claims;

namespace Server.Controllers
{
    [Authorize]
    public class ChatController : Base_Control_Api
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
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
    }
}
using Server.Chat.Dto;
using Server.Chat.Entitys;
using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IChatService
    {
        Task<Message> SendUserMessageAsync(string userId, string adminId, string content);
        Task<(Message message, string targetUserId)> SendAdminReplyAsync(Guid conversationId, string adminId, string content);
        Task<Conversation> GetOrCreateConversationAsync(string userId, string adminId);
        Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(Guid conversationId, string requestingUserId);
        Task<IEnumerable<AdminDto>> GetAdminsAsync();
        Task<IEnumerable<ConversationDto>> GetAdminConversationsAsync(string adminId);
        Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId);
        Task MarkMessagesAsReadAsync(Guid conversationId, string userId);
        Task<int> GetUnreadCountAsync(string userId);
    }
}

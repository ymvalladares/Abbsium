using System.ComponentModel.DataAnnotations;

namespace Server.Chat.Dto
{
    public class SendAdminReplyRequest
    {
        [Required]
        public Guid ConversationId { get; set; }

        [Required]
        [StringLength(5000, MinimumLength = 1)]
        public string Content { get; set; }
    }
}

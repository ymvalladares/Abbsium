using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Server.Entitys;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Chat.Entitys
{
    public class Message
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public string SenderId { get; set; } // FK to AspNetUsers

        [ForeignKey("SenderId")]
        [ValidateNever]
        public User_data User_data { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsAdminMessage { get; set; }

        // Navigation Properties
        public virtual Conversation Conversation { get; set; }

        public Message()
        {
            Id = Guid.NewGuid();
            SentAt = DateTime.UtcNow;
            IsRead = false;
            IsAdminMessage = false;
        }
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Server.Entitys;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Chat.Entitys
{
    public class Conversation
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } // Usuario normal

        [Required]
        [MaxLength(450)]
        public string AdminId { get; set; } // Admin específico con quien chatea

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User { get; set; }

        [ForeignKey("AdminId")]
        [ValidateNever]
        public User_data Admin { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public bool IsActive { get; set; }

        public virtual ICollection<Message> Messages { get; set; }

        public Conversation()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            IsActive = true;
            Messages = new HashSet<Message>();
        }
    }
}

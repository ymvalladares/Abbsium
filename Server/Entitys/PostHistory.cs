using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class PostHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }

        public string Platform { get; set; }

        public bool Success { get; set; }

        public string? PostId { get; set; }

        public string? PostUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }
}

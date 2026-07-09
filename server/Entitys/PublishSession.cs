using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class PublishSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string UserId { get; set; }
        
        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }
        
        public string S3Key { get; set; }
        public string S3Url { get; set; }
        
        public string Caption { get; set; }
        public string Platforms { get; set; } // JSON array: ["facebook", "instagram"]
        
        public string Status { get; set; } = "pending"; // pending, processing, completed, failed
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}

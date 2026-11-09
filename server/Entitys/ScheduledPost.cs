using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class ScheduledPost
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }

        public string S3Key { get; set; }
        public string S3Url { get; set; }
        public string? MediaType { get; set; } // "video" or "photo"
        public string Title { get; set; }
        public string Caption { get; set; }
        public string Platforms { get; set; } // JSON array: ["Facebook", "Instagram"]
        public bool IsShort { get; set; }
        public string? PageId { get; set; }
        public string? YouTubePlaylistId { get; set; }
        public string? ThumbnailUrl { get; set; }

        public DateTime ScheduledFor { get; set; }

        public string Status { get; set; } = "scheduled"; // scheduled, processing, completed, failed, cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string? PublishSessionId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

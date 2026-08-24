using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class VideoClip
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClipJobId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int StartTimeSeconds { get; set; }

        public int EndTimeSeconds { get; set; }

        public int DurationSeconds { get; set; }

        public string? S3Key { get; set; }

        public string? ThumbnailS3Key { get; set; }

        public string? Transcript { get; set; }

        public double? EngagementScore { get; set; }

        public string? Hashtags { get; set; }

        public string? HookText { get; set; }

        public string? BestTimeToPost { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ClipJobId")]
        public ClipJob? ClipJob { get; set; }
    }
}

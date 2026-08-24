using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class ClipJob
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string VideoUrl { get; set; } = string.Empty;

        public string? VideoTitle { get; set; }

        public string? AssemblyTranscriptId { get; set; }

        public string Status { get; set; } = "pending"; // pending, downloading, transcribing, processing, completed, failed

        public int Progress { get; set; }

        public string? ErrorMessage { get; set; }

        public int MaxClips { get; set; } = 5;

        public int MinClipDuration { get; set; } = 30;

        public int MaxClipDuration { get; set; } = 60;

        public string? AspectRatio { get; set; } = "9:16";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public string? LocalVideoPath { get; set; }

        [ForeignKey("UserId")]
        public User_data? User { get; set; }

        public ICollection<VideoClip> Clips { get; set; } = new List<VideoClip>();
    }
}

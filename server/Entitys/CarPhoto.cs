using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class CarPhoto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CarId { get; set; }

        [Required]
        public string S3Key { get; set; } = string.Empty;

        [Required]
        public string S3Url { get; set; } = string.Empty;

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public long? FileSize { get; set; }

        public bool IsCover { get; set; }

        public int Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CarId")]
        public Car? Car { get; set; }
    }
}

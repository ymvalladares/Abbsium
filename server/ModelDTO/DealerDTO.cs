using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class DealerDTO
    {
        public Guid? Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(253)]
        [RegularExpression(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?)*$", ErrorMessage = "Invalid domain format")]
        public string Domain { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Logo { get; set; }

        [MaxLength(7)]
        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Invalid color format. Use hex format like #FF0000")]
        public string? PrimaryColor { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class DealerResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? PrimaryColor { get; set; }
        public string OwnerEmail { get; set; } = string.Empty;
        public string? OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class PublicDealerInfoDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Logo { get; set; }
        public string? PrimaryColor { get; set; }
    }
}

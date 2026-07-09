using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class DealerDTO
    {
        public Guid? Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Domain { get; set; } = string.Empty;

        public string? Logo { get; set; }

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
}

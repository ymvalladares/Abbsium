using System.ComponentModel.DataAnnotations;

namespace Server.Entitys
{
    public class Dealer
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Domain { get; set; } = string.Empty;

        public string? Logo { get; set; }

        public string? PrimaryColor { get; set; }

        [Required]
        public string OwnerEmail { get; set; } = string.Empty;

        public string? OwnerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public ICollection<Car>? Cars { get; set; }
    }
}

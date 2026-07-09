using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class Car
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public Guid DealerId { get; set; }

        public string? UserId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public int Year { get; set; }

        [Required]
        public string Make { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        public string? Trim { get; set; }

        public int? Mileage { get; set; }

        public string? Transmission { get; set; }

        public string? FuelType { get; set; }

        public string? ExteriorColor { get; set; }

        public string? InteriorColor { get; set; }

        public string? Vin { get; set; }

        public string? Description { get; set; }

        public string TitleType { get; set; } = "Clean";

        public string Status { get; set; } = "available";

        public bool Featured { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("DealerId")]
        public Dealer? Dealer { get; set; }

        [ForeignKey("UserId")]
        public User_data? User_data { get; set; }

        public ICollection<CarPhoto>? Photos { get; set; }
    }
}

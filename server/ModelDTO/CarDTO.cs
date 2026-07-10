using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class CarDTO
    {
        public int? Id { get; set; }

        [Required]
        public Guid DealerId { get; set; }

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

        public string? TitleType { get; set; }

        public string Status { get; set; } = "available";

        public bool Featured { get; set; }
    }

    public class CarResponseDTO
    {
        public int Id { get; set; }
        public Guid DealerId { get; set; }
        public string? UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Year { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Trim { get; set; }
        public int? Mileage { get; set; }
        public string? Transmission { get; set; }
        public string? FuelType { get; set; }
        public string? ExteriorColor { get; set; }
        public string? InteriorColor { get; set; }
        public string? Vin { get; set; }
        public string? Description { get; set; }
        public string? TitleType { get; set; }
        public string Status { get; set; } = "available";
        public bool Featured { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<CarPhotoDTO>? Photos { get; set; }
    }

    public class CarPhotoDTO
    {
        public int Id { get; set; }
        public int CarId { get; set; }
        public string S3Key { get; set; } = string.Empty;
        public string S3Url { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
        public bool IsCover { get; set; }
        public int Order { get; set; }
    }

    public class PublicCarResponseDTO
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Year { get; set; }
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Trim { get; set; }
        public int? Mileage { get; set; }
        public string? Transmission { get; set; }
        public string? FuelType { get; set; }
        public string? ExteriorColor { get; set; }
        public string? InteriorColor { get; set; }
        public string? Description { get; set; }
        public string? TitleType { get; set; }
        public string Status { get; set; } = "available";
        public bool Featured { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<CarPhotoDTO>? Photos { get; set; }
    }
}

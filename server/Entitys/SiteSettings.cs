using System.ComponentModel.DataAnnotations;

namespace Server.Entitys
{
    public class SiteSettings
    {
        [Key]
        public int Id { get; set; } = 1;

        public bool IsUnderMaintenance { get; set; } = false;

        public string? MaintenanceMessage { get; set; }

        public DateTime? MaintenanceStartedAt { get; set; }

        public DateTime? LastUpdatedBy { get; set; }
    }
}

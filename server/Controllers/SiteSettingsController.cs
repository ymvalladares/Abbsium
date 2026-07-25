using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;

namespace Server.Controllers
{
    
    public class SiteSettingsController : Base_Control_Api
    {
        private readonly DbContext_app _context;
        private readonly ILogger<SiteSettingsController> _logger;

        public SiteSettingsController(DbContext_app context, ILogger<SiteSettingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSettings { IsUnderMaintenance = false };
                _context.SiteSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                isUnderMaintenance = settings.IsUnderMaintenance,
                maintenanceMessage = settings.MaintenanceMessage,
                maintenanceStartedAt = settings.MaintenanceStartedAt
            });
        }

        [HttpPut]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSettings { IsUnderMaintenance = false };
                _context.SiteSettings.Add(settings);
            }

            var wasUnderMaintenance = settings.IsUnderMaintenance;
            settings.IsUnderMaintenance = request.IsUnderMaintenance;
            settings.MaintenanceMessage = request.MaintenanceMessage;
            settings.LastUpdatedBy = DateTime.UtcNow;

            if (request.IsUnderMaintenance && !wasUnderMaintenance)
            {
                settings.MaintenanceStartedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Admin updated maintenance mode: {Status}",
                request.IsUnderMaintenance ? "ON" : "OFF"
            );

            return Ok(new
            {
                isUnderMaintenance = settings.IsUnderMaintenance,
                maintenanceMessage = settings.MaintenanceMessage,
                maintenanceStartedAt = settings.MaintenanceStartedAt
            });
        }
    }

    public class UpdateSettingsRequest
    {
        public bool IsUnderMaintenance { get; set; }
        public string? MaintenanceMessage { get; set; }
    }
}

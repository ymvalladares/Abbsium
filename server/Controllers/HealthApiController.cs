using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers
{
    public class HealthApiController : Base_Control_Api
    {
        private readonly IS3Service _s3Service;
        private readonly ILogger<HealthApiController> _logger;

        public HealthApiController(IS3Service s3Service, ILogger<HealthApiController> logger)
        {
            _s3Service = s3Service;
            _logger = logger;
        }

        [HttpGet, HttpHead]
        public IActionResult Get()
        {
            return Ok(new { status = "Ok", timestamp = DateTime.UtcNow });
        }

        [HttpGet("s3")]
        public async Task<IActionResult> CheckS3()
        {
            var bucketExists = await _s3Service.VerifyBucketExistsAsync();
            return Ok(new { s3Connected = bucketExists, timestamp = DateTime.UtcNow });
        }
    }
}

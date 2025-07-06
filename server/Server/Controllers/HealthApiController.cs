using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    public class HealthApiController : Base_Control_Api
    {

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "Ok", timestamp = DateTime.UtcNow});
        }
    }
}

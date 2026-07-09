using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Controllers
{
    public class EmailController: Base_Control_Api
    {
        private readonly IEmailSender _emailSender;

        public EmailController(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        [HttpPost]
        public async Task<ActionResult> SendEmail(Email request)
        {
            await _emailSender.SendEmail(request);
            return Ok();
        }
    }
}

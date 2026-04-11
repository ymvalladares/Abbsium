using Server.Entitys;
using Server.Repositories.IRepositories;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;


namespace Server.Services
{
    public class EmailSender
    {
        public class EmailService : IEmailSender
        {
            private readonly IConfiguration _config;

            public EmailService(IConfiguration config)
            {
                _config = config;
            }

            public async Task SendEmail(Email request)
            {
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse("yordan.j.martinez@gmail.com"));
                msg.To.Add(MailboxAddress.Parse(request.To));
                msg.Subject = request.Subject + " " + DateTime.Now.ToString();
                msg.Body = new TextPart(TextFormat.Html) { Text = request.Body };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config.GetSection("Email:Host").Value,
                    Convert.ToInt32(_config.GetSection("Email:Port").Value),
                    SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(
                    _config.GetSection("Email:UserName").Value,
                    _config.GetSection("Email:PassWord").Value);
                await smtp.SendAsync(msg);
                await smtp.DisconnectAsync(true);
            }
        }
    }
}

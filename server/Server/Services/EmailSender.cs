using RestSharp;
using RestSharp.Authenticators;
using Server.Entitys;
using Server.Repositories.IRepositories;


namespace Server.Services
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
            var client = new RestClient("https://api.resend.com");
            var req = new RestRequest("/emails", Method.Post);

            req.AddHeader("Authorization", $"Bearer {_config["Resend:ApiKey"]}");
            req.AddHeader("Content-Type", "application/json");

            req.AddJsonBody(new
            {
                from = "Abbsium <noreply@abbsium.com>",
                to = new[] { request.To },
                subject = request.Subject,
                html = request.Body
            });

            var response = await client.ExecuteAsync(req);

            if (!response.IsSuccessful)
                throw new Exception($"Email failed: {response.Content}");
        }
    }
}

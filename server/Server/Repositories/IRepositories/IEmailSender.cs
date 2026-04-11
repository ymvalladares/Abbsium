using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IEmailSender
    {
        Task SendEmail(Email request);
    }
}

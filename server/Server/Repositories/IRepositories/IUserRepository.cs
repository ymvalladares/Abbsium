using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IUserRepository : IRepository<User_data>
    {
        void Update(User_data user);
    }
}

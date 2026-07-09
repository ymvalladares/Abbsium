using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IDealerRepository : IRepository<Dealer>
    {
        void Update(Dealer dealer);
    }
}

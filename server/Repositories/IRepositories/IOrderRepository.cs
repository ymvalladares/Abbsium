using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IOrderRepository : IRepository<Order>
    {
        void Update(Order order);
    }
}

using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class OrderRepository : Repository<Order>, IOrderRepository
    {
        private readonly DbContext_app _db;

        public OrderRepository(DbContext_app db) : base(db)
        {
           _db = db;
        }
        public void Update(Order order)
        {
            _db.Update(order);
        }
    }
}

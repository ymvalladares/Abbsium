using Server.Data;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContext_app _db;

        public UnitOfWork(DbContext_app db)
        {
            _db = db;
            UserRepository = new UserRepository(_db);

            OrderRepository = new OrderRepository(_db);
        }

        public IUserRepository UserRepository { get; private set; }
        public IOrderRepository OrderRepository { get; private set; }


        public void Save()
        {
           _db.SaveChanges();
        }
    }
}

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

            PaymentHistoryRepository = new PaymentHistoryRepository(_db);

            RefreshTokenRepository = new RefreshTokenRepository(_db);

            DealerRepository = new DealerRepository(_db);

            CarRepository = new CarRepository(_db);

            CarPhotoRepository = new CarPhotoRepository(_db);
        }

        public IUserRepository UserRepository { get; private set; }
        public IOrderRepository OrderRepository { get; private set; }

        public IPaymentHistoryRepository PaymentHistoryRepository { get; private set; }

        public IRefreshTokenRepository RefreshTokenRepository { get; private set; }

        public IDealerRepository DealerRepository { get; private set; }

        public ICarRepository CarRepository { get; private set; }

        public ICarPhotoRepository CarPhotoRepository { get; private set; }


        public void Save()
        {
           _db.SaveChanges();
        }

        public async Task SaveAsync()
        {
           await _db.SaveChangesAsync();
        }
    }
}

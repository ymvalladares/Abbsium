using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class DealerRepository : Repository<Dealer>, IDealerRepository
    {
        private readonly DbContext_app _db;

        public DealerRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }

        public void Update(Dealer dealer)
        {
            _db.Update(dealer);
        }
    }
}

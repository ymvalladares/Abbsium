using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
    {
        private readonly DbContext_app _db;

        public RefreshTokenRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }
    }
}

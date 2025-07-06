using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class UserRepository : Repository<User_data>, IUserRepository
    {
        private readonly DbContext_app _db;

        public UserRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }

        public void Update(User_data user)
        {
            _db.Update(user);
        }
    }
}

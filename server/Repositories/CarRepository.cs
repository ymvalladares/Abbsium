using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class CarRepository : Repository<Car>, ICarRepository
    {
        private readonly DbContext_app _db;

        public CarRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }

        public void Update(Car car)
        {
            _db.Update(car);
        }
    }
}

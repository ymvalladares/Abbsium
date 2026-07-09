using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class CarPhotoRepository : Repository<CarPhoto>, ICarPhotoRepository
    {
        private readonly DbContext_app _db;

        public CarPhotoRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }

        public void Update(CarPhoto carPhoto)
        {
            _db.Update(carPhoto);
        }
    }
}

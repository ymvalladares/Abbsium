using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface ICarPhotoRepository : IRepository<CarPhoto>
    {
        void Update(CarPhoto carPhoto);
    }
}

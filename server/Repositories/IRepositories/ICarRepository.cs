using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface ICarRepository : IRepository<Car>
    {
        void Update(Car car);
    }
}

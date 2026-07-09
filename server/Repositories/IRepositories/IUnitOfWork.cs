namespace Server.Repositories.IRepositories
{
    public interface IUnitOfWork
    {
        IUserRepository UserRepository { get; }
        IOrderRepository OrderRepository { get; }
        IRefreshTokenRepository RefreshTokenRepository { get; }
        IDealerRepository DealerRepository { get; }
        ICarRepository CarRepository { get; }
        ICarPhotoRepository CarPhotoRepository { get; }
        void Save();
        Task SaveAsync();
    }
}

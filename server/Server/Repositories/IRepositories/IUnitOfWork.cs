namespace Server.Repositories.IRepositories
{
    public interface IUnitOfWork
    {
        IUserRepository UserRepository { get; }
        IOrderRepository OrderRepository { get; }
        IRefreshTokenRepository RefreshTokenRepository { get; }
        void Save();
    }
}

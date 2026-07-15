using Server.Entitys;

namespace Server.Repositories.IRepositories
{
    public interface IPaymentHistoryRepository : IRepository<PaymentHistory>
    {
        void Update(PaymentHistory paymentHistory);
    }
}

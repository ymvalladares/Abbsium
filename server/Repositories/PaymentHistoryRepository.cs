using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Repositories
{
    public class PaymentHistoryRepository : Repository<PaymentHistory>, IPaymentHistoryRepository
    {
        private readonly DbContext_app _db;

        public PaymentHistoryRepository(DbContext_app db) : base(db)
        {
            _db = db;
        }

        public void Update(PaymentHistory paymentHistory)
        {
            _db.Update(paymentHistory);
        }
    }
}

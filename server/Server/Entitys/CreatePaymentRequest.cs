namespace Server.Entitys
{
    public class CreatePaymentRequest
    {
        public decimal Amount { get; set; }  // lo manda React: 150, 24.99, 49.99
        public string ServiceType { get; set; }  // "Starter", "Professional", "Enterprise"
        public string Mode { get; set; }  // "payment" o "subscription"
    }
}

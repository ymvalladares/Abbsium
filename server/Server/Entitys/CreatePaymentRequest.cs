namespace Server.Entitys
{
    public class CreatePaymentRequest
    {
        public decimal Amount { get; set; }
        public string ServiceType { get; set; } = string.Empty;
    }
}

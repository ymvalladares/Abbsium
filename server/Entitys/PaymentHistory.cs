using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class PaymentHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrderId { get; set; }

        [ForeignKey("OrderId")]
        [ValidateNever]
        public Order Order { get; set; }

        public string UserId { get; set; } = string.Empty;
        public Guid? DealerId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public string Status { get; set; } = "pending";
        public string StripeInvoiceId { get; set; } = string.Empty;
        public string StripeInvoiceUrl { get; set; } = string.Empty;
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public int FailedAttempts { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}

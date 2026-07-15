using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class Order
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }

        public Guid? DealerId { get; set; }

        [ForeignKey("DealerId")]
        [ValidateNever]
        public Dealer? Dealer { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PaymentIntentId { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
        public string StripeCustomerId { get; set; } = string.Empty;
        public string StripeCustomerEmail { get; set; } = string.Empty;
        public string StripeInvoiceUrl { get; set; } = string.Empty;
        public string StripePaymentMethod { get; set; } = string.Empty;
        public string StripeBrand { get; set; } = string.Empty;
        public string StripeLast4 { get; set; } = string.Empty;
        public string StripeExpMonth { get; set; } = string.Empty;
        public string StripeExpYear { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string ServiceType { get; set; } = string.Empty;
        public string PlanMode { get; set; } = "payment";
        public DateTime? CurrentPeriodStart { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? NextBillingDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public DateTime? TrialEnd { get; set; }
        public int? Quantity { get; set; } = 1;
        public string? Interval { get; set; }
        public int? IntervalCount { get; set; } = 1;
        public bool? CancelAtPeriodEnd { get; set; } = false;
        public DateTime? CancelledAt { get; set; }
        public string CancellationReason { get; set; } = string.Empty;
        public int FailedPaymentAttempts { get; set; } = 0;
        public string LastStripeEvent { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}

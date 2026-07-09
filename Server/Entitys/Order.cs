using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    //add dbContext
    public class Order
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "usd";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string PaymentIntentId { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string ServiceType { get; set; } = string.Empty;
    }
}

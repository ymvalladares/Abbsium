using System;
using System.ComponentModel.DataAnnotations;

namespace Server.Entitys
{
    public class LoginVerificationCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        public string Code { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsUsed { get; set; } = false;

        [MaxLength(450)]
        public string? IpAddress { get; set; }

        public int FailedAttempts { get; set; } = 0;
    }
}

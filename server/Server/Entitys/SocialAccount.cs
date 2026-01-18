using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class SocialAccount
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        [ValidateNever]
        public User_data User_data { get; set; }
        public string Provider { get; set; } // facebook, instagram, youtube, tiktok

        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

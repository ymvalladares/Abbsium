using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Entitys
{
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }

        public string Token { get; set; } = null!;

        public DateTime Expires { get; set; }

        public bool IsRevoked { get; set; } = false;

        public DateTime Created { get; set; } = DateTime.UtcNow;

        // FK to IdentityUser
        public string UserId { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual User_data? User { get; set; }
    }
}

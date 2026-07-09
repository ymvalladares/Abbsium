using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class LoginDTO
    {
        [EmailAddress]
        [Required]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }
}

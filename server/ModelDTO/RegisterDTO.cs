using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class RegisterDTO
    {
        [Required]
        [MaxLength(100)]
        public string UserName { get; set; }

        [Required, EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; }

        [Required, MinLength(8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$", ErrorMessage = "Password must be at least 8 characters with uppercase, lowercase, and a number")]
        public string Password { get; set; }

    }
}

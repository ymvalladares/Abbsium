using System.ComponentModel.DataAnnotations;

namespace Server.ModelDTO
{
    public class RegisterDTO
    {
        [Required]
        public string UserName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }

    }
}

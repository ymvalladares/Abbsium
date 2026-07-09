namespace Server.ModelDTO
{
    public class UserModelDto
    {
        public string?Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? Role { get; set; }
    }
}

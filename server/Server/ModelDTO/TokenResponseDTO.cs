namespace Server.ModelDTO
{
    public class TokenResponseDTO
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public string? Rol { get; set; }
    }
}

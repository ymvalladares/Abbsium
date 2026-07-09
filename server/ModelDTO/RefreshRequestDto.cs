namespace Server.ModelDTO
{
    public class RefreshRequestDto
    {
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}

namespace Server.ModelDTO
{
    public class AiResponseDto
    {
        public string Role { get; set; } = "assistant";
        public string? Content { get; set; }
        public string? ImageUrl { get; set; }
    }
}

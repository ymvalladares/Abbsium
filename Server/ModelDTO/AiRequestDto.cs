namespace Server.ModelDTO
{
    public class AiRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        // Historial de conversación
        public List<ChatMessageDto>? Messages { get; set; }
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = string.Empty; // "user" o "assistant"
        public string Content { get; set; } = string.Empty;
    }
}

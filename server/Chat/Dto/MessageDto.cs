namespace Server.Chat.Dto
{
    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsAdminMessage { get; set; }
        public bool IsRead { get; set; }
        public string SenderName { get; set; }
    }
}

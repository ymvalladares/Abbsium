namespace Server.ModelDTO
{
    public class SocialAccountDTO
    {
        public string Provider { get; set; }
        public bool Connected { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}

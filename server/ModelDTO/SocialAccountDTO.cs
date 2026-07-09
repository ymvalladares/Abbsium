namespace Server.ModelDTO
{
    public class SocialAccountDTO
    {
        public Guid Id { get; set; }
        public string Provider { get; set; }
        public bool Connected { get; set; }
        public bool IsActive { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? ProviderAccountId { get; set; }
        public string? Scope { get; set; }
        public string? AccountName { get; set; }
    }
}

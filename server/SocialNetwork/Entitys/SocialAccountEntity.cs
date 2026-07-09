namespace Server.SocialNetwork.Entitys
{
    public class SocialAccountEntity
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }   // TU usuario (JWT)
        public string Provider { get; set; } // Facebook, Instagram, etc
        public string AccessToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string ExternalAccountId { get; set; } // Page ID, IG ID
    }
}

using Server.Models.SocialMedia.Enums;

namespace Server.Models.SocialMedia.Responses
{
    public class SocialPostResult
    {
        public SocialPlatform Platform { get; set; }
        public bool Success { get; set; }
        public string? PostId { get; set; }
        public string? PostUrl { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }

    public class MultiPlatformPostResult
    {
        public List<SocialPostResult> Results { get; set; } = new();
        public int TotalPlatforms => Results.Count;
        public int SuccessfulPosts => Results.Count(r => r.Success);
        public int FailedPosts => Results.Count(r => !r.Success);
        public bool AllSuccessful => FailedPosts == 0;
    }
}

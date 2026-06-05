using Server.Models.SocialMedia.Enums;
using System.ComponentModel.DataAnnotations;

namespace Server.Models.SocialMedia.Requests
{
    public class SocialPostRequest
    {
        [Required]
        public List<SocialPlatform> Platforms { get; set; } = new();

        public string? Message { get; set; }
        public string? Caption { get; set; }
        public string? PhotoUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? ThumbnailUrl { get; set; }

        public string? PageId { get; set; }
        public string? YouTubePlaylistId { get; set; }
        public bool IsShort { get; set; }
    }
}

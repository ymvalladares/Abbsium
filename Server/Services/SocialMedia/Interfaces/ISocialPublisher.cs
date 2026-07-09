using Server.Models.SocialMedia.Enums;
using Server.Models.SocialMedia.Requests;
using Server.Models.SocialMedia.Responses;

namespace Server.Services.SocialMedia.Interfaces
{
    public interface ISocialPublisher
    {
        SocialPlatform Platform { get; }
        Task<SocialPostResult> PublishTextAsync(string userId, SocialPostRequest request);
        Task<SocialPostResult> PublishPhotoAsync(string userId, SocialPostRequest request);
        Task<SocialPostResult> PublishVideoAsync(string userId, SocialPostRequest request);
    }
}

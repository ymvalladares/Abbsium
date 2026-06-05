using Server.Models.SocialMedia.Enums;

namespace Server.Services.SocialMedia.Interfaces
{
    public interface ISocialAuthService
    {
        SocialPlatform Platform { get; }
        string GetAuthorizationUrl(string userId);
        Task HandleCallbackAsync(string code, string state, string userId);
        Task RefreshTokenAsync(string userId);
        Task<bool> IsTokenValidAsync(string userId);
        Task DisconnectAsync(string userId);
    }
}

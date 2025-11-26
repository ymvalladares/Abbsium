using Microsoft.AspNetCore.Identity;
using Server.Entitys;
using System.Security.Claims;

namespace Server.Repositories.IRepositories
{
    public interface ITokenService
    {
        // Task<string> CreateToken(IdentityUser user);

        Task<string> CreateToken(IdentityUser user);               // JWT (access)
        string CreateRefreshToken();                               // generar refresh token
        Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(IdentityUser user, TimeSpan? ttl = null);
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
        Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(RefreshToken token);
        Task RevokeAllTokensForUserAsync(string userId);
    }
}

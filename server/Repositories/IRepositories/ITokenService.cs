using Server.Entitys;
using System.Security.Claims;

public interface ITokenService
{
    Task<string> CreateToken(User_data user);

    Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken);

    Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(User_data user, TimeSpan? ttl = null);

    Task DeleteRefreshTokenAsync(RefreshToken token);

    Task CleanupExpiredRefreshTokensAsync();

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token); // opcional si aún lo usas

    Task RevokeTokensForUserAsync(string userId);

}

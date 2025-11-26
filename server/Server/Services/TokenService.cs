using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Server.Repositories.IRepositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Entitys;

namespace Server.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly DbContext_app _dbContext;

        public TokenService(IConfiguration configuration, UserManager<IdentityUser> userManager, DbContext_app dbContext)
        {
            _configuration = configuration;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        // -----------------------------------------------------------------
        // ACCESS TOKEN (JWT)
        // -----------------------------------------------------------------
        public async Task<string> CreateToken(IdentityUser user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            double expiresMinutes = double.Parse(jwtSettings["ExpiresInMinutes"] ?? "5");

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // -----------------------------------------------------------------
        // REFRESH TOKEN
        // -----------------------------------------------------------------
        public string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        public async Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(IdentityUser user, TimeSpan? ttl = null)
        {
            double ttlMinutes = ttl?.TotalMinutes ?? 5;

            // Revocar/eliminar tokens anteriores
            var oldTokens = await _dbContext.RefreshTokens
                .Where(r => r.UserId == user.Id)
                .ToListAsync();

            foreach (var token in oldTokens)
                token.IsRevoked = true;

            await _dbContext.SaveChangesAsync();

            var refresh = new RefreshToken
            {
                Token = CreateRefreshToken(),
                UserId = user.Id,
                Created = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(ttlMinutes),
                IsRevoked = false
            };

            _dbContext.RefreshTokens.Add(refresh);
            await _dbContext.SaveChangesAsync();

            return refresh;
        }

        public async Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken)
        {
            return await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken);
        }

        public async Task RevokeRefreshTokenAsync(RefreshToken token)
        {
            token.IsRevoked = true;
            await _dbContext.SaveChangesAsync();
        }

        // -----------------------------------------------------------------
        // READ EXPIRED ACCESS TOKEN
        // -----------------------------------------------------------------
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));

            var parameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false // <- PERMITE LEER TOKENS EXPIRADOS
            };

            var handler = new JwtSecurityTokenHandler();

            try
            {
                return handler.ValidateToken(token, parameters, out var validatedToken);
            }
            catch
            {
                return null;
            }
        }

        // -----------------------------------------------------------------
        // DELETE ALL TOKENS FOR USER
        // -----------------------------------------------------------------

        public async Task RevokeAllTokensForUserAsync(string userId)
        {
            var tokens = _dbContext.RefreshTokens
                .Where(t => t.UserId == userId)
                .ToList();

            foreach (var token in tokens)
            {
                _dbContext.RefreshTokens.Remove(token);
            }

            await _dbContext.SaveChangesAsync();
        }

    }
}

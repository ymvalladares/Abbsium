using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Entitys;
using Server.Repositories.IRepositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Server.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<User_data> _userManager;
        private readonly DbContext_app _dbContext;

        public TokenService(
            IConfiguration configuration,
            UserManager<User_data> userManager,
            DbContext_app dbContext)
        {
            _configuration = configuration;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        // -----------------------------------------------------------------
        // ACCESS TOKEN (JWT)
        // -----------------------------------------------------------------
        public async Task<string> CreateToken(User_data user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Key"])
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            double expiresMinutes = double.Parse(jwtSettings["ExpiresInMinutes"] ?? "10");

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
        // REFRESH TOKEN GENERATION
        // -----------------------------------------------------------------
        private string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        public async Task<RefreshToken> GenerateAndSaveRefreshTokenAsync(
            User_data user,
            TimeSpan? ttl = null)
        {
            // 👉 Refresh token dura 7 días por defecto
            var expires = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromDays(7));

            var refresh = new RefreshToken
            {
                Token = CreateRefreshToken(),
                UserId = user.Id,
                Created = DateTime.UtcNow,
                Expires = expires,
                IsRevoked = false
            };

            _dbContext.RefreshTokens.Add(refresh);
            await _dbContext.SaveChangesAsync();

            return refresh;
        }

        // -----------------------------------------------------------------
        // DB OPERATIONS
        // -----------------------------------------------------------------
        public async Task<RefreshToken?> GetRefreshTokenEntityAsync(string refreshToken)
        {
            return await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == refreshToken);
        }

        public async Task DeleteRefreshTokenAsync(RefreshToken token)
        {
            _dbContext.RefreshTokens.Remove(token);
            await _dbContext.SaveChangesAsync();
        }

        // -----------------------------------------------------------------
        // OPTIONAL: CLEANUP EXPIRED TOKENS (CRON / BACKGROUND JOB)
        // -----------------------------------------------------------------
        public async Task CleanupExpiredRefreshTokensAsync()
        {
            var now = DateTime.UtcNow;

            var expired = await _dbContext.RefreshTokens
                .Where(t => t.Expires <= now)
                .ToListAsync();

            if (expired.Count > 0)
            {
                _dbContext.RefreshTokens.RemoveRange(expired);
                await _dbContext.SaveChangesAsync();
            }
        }

        // -----------------------------------------------------------------
        // READ EXPIRED ACCESS TOKEN (OPTIONAL — YA NO NECESARIO PARA REFRESH)
        // -----------------------------------------------------------------
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"])
            );

            var parameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = false
            };

            var handler = new JwtSecurityTokenHandler();

            try
            {
                return handler.ValidateToken(token, parameters, out _);
            }
            catch
            {
                return null;
            }
        }

        public async Task RevokeTokensForUserAsync(string userId)
        {
            // Buscar todos los tokens del usuario
            var tokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (tokens.Any())
            {
                // Eliminar de la DB
                _dbContext.RefreshTokens.RemoveRange(tokens);
                await _dbContext.SaveChangesAsync();
            }
        }

    }
}

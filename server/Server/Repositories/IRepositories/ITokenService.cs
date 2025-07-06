using Microsoft.AspNetCore.Identity;

namespace Server.Repositories.IRepositories
{
    public interface ITokenService
    {
        Task<string> CreateToken(IdentityUser user);
    }
}

using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;
using System.Net;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Server.Controllers
{
    public class AccountController : Base_Control_Api
    {
        private readonly DbContext_app _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;

        public AccountController(
            DbContext_app db,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenService,
            IConfiguration config)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDTO>> Register(RegisterDTO model)
        {
            // 1) Verificar email duplicado
            if (await _userManager.FindByEmailAsync(model.Email) is not null)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Email is already registered."
                });
            }

            // 2) Crear rol por defecto si no existe
            await EnsureDefaultRolesAsync();

            // 3) Crear el usuario
            var newUser = new User_data
            {
                UserName = model.UserName,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(newUser, model.Password);

            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = $"User creation failed: {errors}"
                });
            }

            // 4) Asignar rol por defecto
            await _userManager.AddToRoleAsync(newUser, Roles.Role_Admin);

            // 5) Respuesta
            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "User registered successfully."
            });
        }


        /// <summary>
        /// Verifica y crea el rol de usuario por defecto si no existe.
        /// </summary>
        private async Task EnsureDefaultRolesAsync()
        {
            if (!await _roleManager.RoleExistsAsync(Roles.Role_User))
            {
                await _roleManager.CreateAsync(new IdentityRole(Roles.Role_User));
            }
        }


        [HttpPost("login")]
        public async Task<ActionResult<TokenResponseDTO>> Login(LoginDTO model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid email."
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid password."
                });
            }

            // Generar access token (JWT)
            var token = await _tokenService.CreateToken(user);

            // Generar y almacenar refresh token en DB
            var refreshEntity = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new TokenResponseDTO
            {
                Token = token,
                RefreshToken = refreshEntity.Token,
                Email = user.Email,
                UserName = user.UserName,
                Rol = roles.FirstOrDefault() ?? "User"
            });
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<TokenResponseDTO>> GoogleLogin([FromBody] string idToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                var user = await _userManager.FindByEmailAsync(payload.Email);

                if (user == null)
                {
                    user = new User_data
                    {
                        Email = payload.Email,
                        UserName = payload.Email,
                        EmailConfirmed = true
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                        return BadRequest(new AuthResponseDTO
                        {
                            Success = false,
                            Message = $"No se pudo crear el usuario con Google: {errors}"
                        });
                    }

                    // Asignar rol por defecto
                    await _userManager.AddToRoleAsync(user, Roles.Role_User);
                }
                else
                {
                    var hasPassword = await _userManager.HasPasswordAsync(user);
                    if (hasPassword)
                    {
                        return BadRequest(new AuthResponseDTO
                        {
                            Success = false,
                            Message = "Use the traditional login for this email"
                        });
                    }
                }

                // Generar tokens (access + refresh)
                var token = await _tokenService.CreateToken(user);
                var refreshEntity = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);
                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new TokenResponseDTO
                {
                    Token = token,
                    RefreshToken = refreshEntity.Token,
                    Email = user.Email,
                    UserName = user.UserName,
                    Rol = roles.FirstOrDefault() ?? "User"
                });
            }
            catch (InvalidJwtException)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Token de Google inválido o expirado."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponseDTO
                {
                    Success = false,
                    Message = $"Error interno: {ex.Message}"
                });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh(RefreshRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { success = false, message = "Invalid request." });

            var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
                return BadRequest(new { success = false, message = "Invalid access token." });

            var userId =
             principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
             principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, message = "Access token is missing the 'sub' claim (userId)." });
            }

            var stored = await _tokenService.GetRefreshTokenEntityAsync(request.RefreshToken);

            if (stored == null || stored.UserId != userId)
                return Unauthorized(new { success = false, message = "Invalid refresh token." });

            if (stored.IsRevoked || stored.Expires <= DateTime.UtcNow)
                return Unauthorized(new { success = false, message = "Token revoked/expired." });

            // Revocar el refresh token viejo
            await _tokenService.RevokeRefreshTokenAsync(stored);

            // Crear nuevos
            var user = await _userManager.FindByIdAsync(userId);
            var newAccess = await _tokenService.CreateToken(user);
            var newRefresh = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);

            return Ok(new TokenResponseDTO
            {
                Token = newAccess,
                RefreshToken = newRefresh.Token,
                Email = user.Email,
                UserName = user.UserName,
                Rol = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "User"
            });
        }


        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequestDto req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.RefreshToken))
                return BadRequest(new { success = false, message = "Missing refresh token." });

            var stored = await _tokenService.GetRefreshTokenEntityAsync(req.RefreshToken);
            if (stored == null)
                return NotFound(new { success = false, message = "Refresh token not found." });

            // 🔥 Obtener userId del token
            var userId = stored.UserId;

            // 🔥 BORRAR TODOS los tokens del usuario
            await _tokenService.RevokeAllTokensForUserAsync(userId);

            return Ok(new { success = true, message = "All refresh tokens deleted." });
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult<AuthResponseDTO>> ResetPassword(ResetPasswordDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid reset data."
                });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid email."
                });
            }

            var resetResult = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(" ", resetResult.Errors.Select(e => e.Description));

                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = $"Password reset failed: {errors}"
                });
            }

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Password has been reset successfully."
            });
        }

        [HttpPost("forgetPassword")]
        public async Task<ActionResult<AuthResponseDTO>> ForgotPassword(ForgotPasswordDTO model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return Ok(new AuthResponseDTO
                {
                    Success = true,
                    Message = "If an account exists for that email, you will receive a reset link."
                });
            }

            //var token = await _user_manager.GeneratePasswordResetTokenAsync(user);
            //var encodedToken = WebUtility.UrlEncode(token);

            //var resetLink = $"{_config["FrontendUrl"]}/reset-password?email={user.Email}&token={encodedToken}";

            // Aquí llamas a tu servicio de email
            //await _emailService.SendAsync(user.Email, "Password Reset", $"Reset your password: {resetLink}");

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Reset link sent. Please check your email."
            });
        }

        // ---------- Helpers internos ----------

        // Evita typo al crear token (centraliza para mantener nombre correcto)
        private async Task<string> _token_service_create_token_safe(IdentityUser user)
        {
            return await _tokenService.CreateToken(user);
        }
    }
}

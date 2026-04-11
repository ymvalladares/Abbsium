using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Server.Data;
using Server.EmailTemplates;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;
using Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Server.Controllers
{
    public class AccountController : Base_Control_Api
    {
        private readonly DbContext_app _db;
        private readonly UserManager<User_data> _userManager;
        private readonly SignInManager<User_data> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _config;
        private readonly IEmailSender _emailSender;

        public AccountController(
        DbContext_app db,
        UserManager<User_data> userManager,
        SignInManager<User_data> signInManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        IConfiguration config,
        IEmailSender emailSender)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _config = config;
            _emailSender = emailSender;
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

            // Generar token de confirmación
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(newUser);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            // Construir link
            var confirmLink = $"{_config["ClientUrl"]}/email-confirmed?userId={newUser.Id}&token={encodedToken}";

            // Enviar email
            await _emailSender.SendEmail(new Email
            {
                To = model.Email,
                Subject = "Confirm your Abbsium account",
                Body = EmailConfirmationTemplate.Build(confirmLink)
            });

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
            await _userManager.AddToRoleAsync(newUser, Roles.Role_User);

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
                EmailConfirmed = user.EmailConfirmed,
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
                    EmailConfirmed = user.EmailConfirmed,
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
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { success = false, message = "Refresh token is required." });

            // 1. Buscar refresh token en DB
            var stored = await _tokenService.GetRefreshTokenEntityAsync(request.RefreshToken);

            if (stored == null)
                return Unauthorized(new { success = false, message = "Invalid refresh token." });

            // 2. Verificar expiración y revocación
            if (stored.IsRevoked || stored.Expires <= DateTime.UtcNow)
            {
                await _tokenService.DeleteRefreshTokenAsync(stored);
                return Unauthorized(new { success = false, message = "Refresh token expired or revoked." });
            }

            // 3. Obtener usuario desde refresh token
            var user = await _userManager.FindByIdAsync(stored.UserId);

            if (user == null)
            {
                await _tokenService.DeleteRefreshTokenAsync(stored);
                return Unauthorized(new { success = false, message = "User not found." });
            }

            // 4. Eliminar refresh token usado (rotación)
            await _tokenService.DeleteRefreshTokenAsync(stored);

            // 5. Crear nuevos tokens
            var newAccess = await _tokenService.CreateToken(user);
            var newRefresh = await _tokenService.GenerateAndSaveRefreshTokenAsync(user);

            return Ok(new TokenResponseDTO
            {
                Token = newAccess,
                RefreshToken = newRefresh.Token,
                Email = user.Email,
                UserName = user.UserName,
                EmailConfirmed = user.EmailConfirmed,
                Rol = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "User"
            });
        }


        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { success = false, message = "User not found in token." });

            await _tokenService.RevokeTokensForUserAsync(userId);

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


        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

            if (!result.Succeeded)
                return BadRequest("Invalid or expired token");

            return Ok();
        }


        // ---------- Helpers internos ----------

        // Evita typo al crear token (centraliza para mantener nombre correcto)
        private async Task<string> _token_service_create_token_safe(User_data user)
        {
            return await _tokenService.CreateToken(user);
        }
    }
}

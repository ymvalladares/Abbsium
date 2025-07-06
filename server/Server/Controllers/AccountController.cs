using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Data;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;
using System.Net;

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

        public AccountController(DbContext_app db, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, RoleManager<IdentityRole> roleManager, ITokenService tokenService, IConfiguration config)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _config = config;
        }

        [HttpGet("test-users")]
        public IActionResult TestUsers()
        {
            var users = _db.Users.ToList();
            return Ok(new { count = users.Count, data = users });
        }


        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO register_user)
        {
            // Verifica si el usuario ya existe
            if (await _userManager.FindByEmailAsync(register_user.Email) is not null)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Email is already registered."
                });
            }

            // Asegura que el rol 'User' exista
            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Crear el nuevo usuario
            var user = new User_data
            {
                UserName = register_user.UserName,
                Email = register_user.Email
            };

            var result = await _userManager.CreateAsync(user, register_user.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = $"User creation failed: {errors}"
                });
            }

            // Asignar rol por defecto
            await _userManager.AddToRoleAsync(user, Roles.Role_User);

            // Preparar respuesta
            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "User registered successfully.",
            });
        }


        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid login data."
                });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return Unauthorized(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid email !!!."
                });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(new AuthResponseDTO
                {
                    Success = false,
                    Message = "Invalid password !!!."
                });
            }

            // Generar token
            var token =  await _tokenService.CreateToken(user);

            return Ok(new UserDTO
            {
               UserName = user.UserName,
               Email = user.Email,
               Token = token,
            });
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<UserDTO>> GoogleLogin([FromBody] string idToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                var user = await _userManager.FindByEmailAsync(payload.Email);

                if (user == null)
                {
                    user = new IdentityUser
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

                    await _userManager.AddToRoleAsync(user, Roles.Role_User);
                }
                else
                {
                    var hasPassword = await _userManager.HasPasswordAsync(user);
                    if (hasPassword)
                    {
                        return Unauthorized(new AuthResponseDTO
                        {
                            Success = false,
                            Message = "Use the traditional login for this email"
                        });
                    }
                }

                var token = await _tokenService.CreateToken(user);

                return Ok(new UserDTO
                {
                    Email = user.Email,
                    UserName = user.UserName,
                    Token = token
                });
            }
            catch (InvalidJwtException)
            {
                return Unauthorized(new AuthResponseDTO
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
                return Unauthorized(new AuthResponseDTO
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

        [HttpPost("forgot-password")]
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

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            var resetLink = $"{_config["FrontendUrl"]}/reset-password?email={user.Email}&token={encodedToken}";

            // Aquí llamas a tu servicio de email
            //await _emailService.SendAsync(user.Email, "Password Reset", $"Reset your password: {resetLink}");

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Reset link sent. Please check your email."
            });
        }

    }
}

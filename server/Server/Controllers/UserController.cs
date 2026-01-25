using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;

namespace Server.Controllers
{
    //[Authorize(Roles = Roles.Role_Admin)]
    [Authorize]
    public class UserController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly UserManager<User_data> _userManager;

        public UserController(IUnitOfWork unitOfWork, IMapper mapper, UserManager<User_data> userManager)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _userManager = userManager;
        }

        [HttpGet("All-Users")]
        public async Task<ActionResult<IEnumerable<UserModelDto>>> getAllUser()
        {
            var users = _unitOfWork.UserRepository.GetAll();
            if (users == null || !users.Any()) return BadRequest("Users not found");

            var userDtos = new List<UserModelDto>();

            foreach (var user in users)
            {
                var dto = _mapper.Map<UserModelDto>(user);
                var roles = await _userManager.GetRolesAsync(user);
                dto.Role = roles.FirstOrDefault();
                userDtos.Add(dto);
            }

            return Ok(userDtos);
        }

        [HttpGet("ById/{id}")]
        public async Task<ActionResult<UserModelDto>> getUserById(string id)
        {
            var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.Id == id);
            if (user == null) return NotFound("User not found");

            var dto = _mapper.Map<UserModelDto>(user);
            var roles = await _userManager.GetRolesAsync(user);
            dto.Role = roles.FirstOrDefault();

            return Ok(dto);
        }

        [HttpGet("ByUserName/{userName}")]
        public async Task<ActionResult<UserModelDto>> getUserByUserName(string userName)
        {
            var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.UserName == userName);
            if (user == null) return NotFound("User not found");

            var dto = _mapper.Map<UserModelDto>(user);
            var roles = await _userManager.GetRolesAsync(user);
            dto.Role = roles.FirstOrDefault();

            return Ok(dto);
        }

        [HttpPost("Upsert")]
        public async Task<ActionResult<UserModelDto>> upsertUser([FromBody] UserModelDto upsertDto)
        {
            try
            {
                // ✅ MODO UPDATE - Si trae ID
                if (!string.IsNullOrEmpty(upsertDto.Id))
                {
                    var existingUser = await _userManager.FindByIdAsync(upsertDto.Id);
                    if (existingUser == null) return NotFound("User not found");

                    // Actualizar solo Username (Email y Role están bloqueados en edit mode)
                    if (!string.IsNullOrWhiteSpace(upsertDto.Username) && existingUser.UserName != upsertDto.Username)
                    {
                        // Validar que el nuevo username no exista
                        var usernameExists = await _userManager.FindByNameAsync(upsertDto.Username);
                        if (usernameExists != null && usernameExists.Id != existingUser.Id)
                        {
                            return BadRequest("Username already exists");
                        }

                        existingUser.UserName = upsertDto.Username;
                        var updateResult = await _userManager.UpdateAsync(existingUser);

                        if (!updateResult.Succeeded)
                        {
                            return BadRequest(new { errors = updateResult.Errors.Select(e => e.Description) });
                        }
                    }

                    return Ok(new
                    {
                        message = "User update with password Abbsium.2020"
                    });
                }
                // ✅ MODO CREATE - Si NO trae ID
                else
                {
                    var emailExists = await _userManager.FindByEmailAsync(upsertDto.Email);
                    if (emailExists != null) return BadRequest("Email already exists");

                    var usernameExists = await _userManager.FindByNameAsync(upsertDto.Username);
                    if (usernameExists != null) return BadRequest("Username already exists");

                    if (string.IsNullOrWhiteSpace(upsertDto.Role))
                    {
                        return BadRequest("Role is required");
                    }

                    // Crear nuevo usuario
                    var newUser = new User_data
                    {
                        UserName = upsertDto.Username,
                        Email = upsertDto.Email,
                        EmailConfirmed = true
                    };

                    // ✅ Contraseña por defecto
                    const string defaultPassword = "Abbsium.2020";

                    // Crear usuario con UserManager
                    var createResult = await _userManager.CreateAsync(newUser, defaultPassword);

                    if (!createResult.Succeeded)
                    {
                        return BadRequest(new { errors = createResult.Errors.Select(e => e.Description) });
                    }

                    // Asignar rol al nuevo usuario
                    var roleResult = await _userManager.AddToRoleAsync(newUser, upsertDto.Role);
                    if (!roleResult.Succeeded)
                    {
                        // Si falla la asignación del rol, eliminar el usuario creado
                        await _userManager.DeleteAsync(newUser);
                        return BadRequest(new { errors = roleResult.Errors.Select(e => e.Description) });
                    }

                    return Ok(new
                    {
                        message = "User created successfully with default password: Abbsium.2020"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> deleteUser(string id)
        {
            // Validar que el usuario existe
            var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.Id == id);
            if (user == null) return NotFound("User not found");

            // Prevenir que el usuario se elimine a sí mismo
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (currentUserId == id)
            {
                return BadRequest("Cannot delete your own account");
            }

            // Usar UserManager para eliminar (maneja roles y claims automáticamente)
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
            }

            return Ok(new { message = "User deleted successfully" });
        }

        // ✅ ENDPOINT PARA ELIMINAR MÚLTIPLES USUARIOS (BULK DELETE)
        [HttpDelete("Delete-Multiple")]
        public async Task<ActionResult> deleteMultipleUsers([FromBody] List<string> userIds)
        {
            if (userIds == null || !userIds.Any())
            {
                return BadRequest("No user IDs provided");
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var userId in userIds)
            {
                // Prevenir que el usuario se elimine a sí mismo
                if (currentUserId == userId)
                {
                    errors.Add($"Cannot delete your own account (ID: {userId})");
                    continue;
                }

                var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.Id == userId);
                if (user == null)
                {
                    errors.Add($"User not found (ID: {userId})");
                    continue;
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    deletedCount++;
                }
                else
                {
                    errors.Add($"Failed to delete user {user.UserName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            return Ok(new
            {
                message = $"{deletedCount} user(s) deleted successfully",
                deletedCount,
                errors
            });
        }
    }
}
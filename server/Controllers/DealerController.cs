using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;
using Server.Services;
using System.Security.Claims;

namespace Server.Controllers
{
    [Authorize]
    public class DealerController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<User_data> _userManager;
        private readonly IS3Service _s3Service;

        public DealerController(IUnitOfWork unitOfWork, UserManager<User_data> userManager, IS3Service s3Service)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _s3Service = s3Service;
        }

        [HttpGet("All-Dealers")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<DealerResponseDTO>>> getAllDealers()
        {
            var dealers = await _unitOfWork.DealerRepository.GetAllAsync();
            if (dealers == null || !dealers.Any()) return Ok(new List<DealerResponseDTO>());

            var dealerDtos = new List<DealerResponseDTO>();

            foreach (var dealer in dealers)
            {
                var dto = new DealerResponseDTO
                {
                    Id = dealer.Id,
                    Name = dealer.Name,
                    Domain = dealer.Domain,
                    Logo = dealer.Logo,
                    PrimaryColor = dealer.PrimaryColor,
                    OwnerEmail = dealer.OwnerEmail,
                    OwnerId = dealer.OwnerId,
                    CreatedAt = dealer.CreatedAt,
                    IsActive = dealer.IsActive
                };
                dealerDtos.Add(dto);
            }

            return Ok(dealerDtos);
        }

        [HttpGet("ById/{id}")]
        public async Task<ActionResult<DealerResponseDTO>> getDealerById(Guid id)
        {
            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == id);
            if (dealer == null) return NotFound("Dealer not found");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
            if (!roles.Contains(Roles.Role_Admin) && dealer.OwnerId != currentUserId)
                return Unauthorized("You do not have permission to view this dealer");

            var dto = new DealerResponseDTO
            {
                Id = dealer.Id,
                Name = dealer.Name,
                Domain = dealer.Domain,
                Logo = dealer.Logo,
                PrimaryColor = dealer.PrimaryColor,
                OwnerEmail = dealer.OwnerEmail,
                OwnerId = dealer.OwnerId,
                CreatedAt = dealer.CreatedAt,
                IsActive = dealer.IsActive
            };

            return Ok(dto);
        }

        [HttpGet("ByDomain/{domain}")]
        public async Task<ActionResult<DealerResponseDTO>> getDealerByDomain(string domain)
        {
            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Domain == domain);
            if (dealer == null) return NotFound("Dealer not found");

            var dto = new DealerResponseDTO
            {
                Id = dealer.Id,
                Name = dealer.Name,
                Domain = dealer.Domain,
                Logo = dealer.Logo,
                PrimaryColor = dealer.PrimaryColor,
                OwnerEmail = dealer.OwnerEmail,
                OwnerId = dealer.OwnerId,
                CreatedAt = dealer.CreatedAt,
                IsActive = dealer.IsActive
            };

            return Ok(dto);
        }

        [HttpGet("My-Dealer")]
        public async Task<ActionResult<DealerResponseDTO>> getMyDealer()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("Invalid token");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.OwnerId == currentUserId);
            if (dealer == null) return Ok((DealerResponseDTO)null);

            var dto = new DealerResponseDTO
            {
                Id = dealer.Id,
                Name = dealer.Name,
                Domain = dealer.Domain,
                Logo = dealer.Logo,
                PrimaryColor = dealer.PrimaryColor,
                OwnerEmail = dealer.OwnerEmail,
                OwnerId = dealer.OwnerId,
                CreatedAt = dealer.CreatedAt,
                IsActive = dealer.IsActive
            };

            return Ok(dto);
        }

        [HttpPost("Upsert")]
        public async Task<ActionResult<AuthResponseDTO>> upsertDealer([FromBody] DealerDTO upsertDto)
        {
            try
            {
                // 🔐 ID del usuario logueado desde el JWT
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized("Invalid token");

                var currentUser = await _userManager.FindByIdAsync(currentUserId);
                if (currentUser == null)
                    return Unauthorized("User not found");

                var roles = await _userManager.GetRolesAsync(currentUser);
                var isAdmin = roles.Contains(Roles.Role_Admin);
                var isDealer = roles.Contains(Roles.Role_Dealer);

                if (!isAdmin && !isDealer)
                    return Unauthorized("You do not have permission to manage dealers");

                // ✅ MODO UPDATE - Si trae ID (solo Admin)
                if (upsertDto.Id.HasValue)
                {
                    if (!isAdmin)
                        return Unauthorized("Only admins can update dealers");

                    var existingDealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == upsertDto.Id.Value);
                    if (existingDealer == null) return NotFound("Dealer not found");

                    // Actualizar campos
                    if (!string.IsNullOrWhiteSpace(upsertDto.Name))
                    {
                        existingDealer.Name = upsertDto.Name;
                    }

                    existingDealer.Logo = upsertDto.Logo;
                    existingDealer.PrimaryColor = upsertDto.PrimaryColor;
                    existingDealer.IsActive = upsertDto.IsActive;

                    _unitOfWork.DealerRepository.Update(existingDealer);
                    await _unitOfWork.SaveAsync();

                    return Ok(new AuthResponseDTO
                    {
                        Success = true,
                        Message = "Dealer updated successfully"
                    });

                }
                // ✅ MODO CREATE - Si NO trae ID (Dealer user crea su propio dealer)
                else
                {
                    // Validar que el usuario no tenga ya un dealer
                    var existingDealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.OwnerId == currentUserId);
                    if (existingDealer != null)
                        return BadRequest("You already have a dealer associated with your account");

                    var domainExists = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Domain.ToLower() == upsertDto.Domain.ToLower());
                    if (domainExists != null) return BadRequest("Domain already exists");

                    var newDealer = new Dealer
                    {
                        Id = Guid.NewGuid(),
                        Name = upsertDto.Name,
                        Domain = upsertDto.Domain,
                        Logo = upsertDto.Logo,
                        PrimaryColor = upsertDto.PrimaryColor,
                        OwnerEmail = currentUser.Email,
                        OwnerId = currentUserId,
                        IsActive = true
                    };

                    _unitOfWork.DealerRepository.Add(newDealer);
                    await _unitOfWork.SaveAsync();

                    return Ok(new AuthResponseDTO
                    {
                        Success = true,
                        Message = "Dealer created successfully",
                        Data = new { dealerId = newDealer.Id }
                    });

                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpDelete("Delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> deleteDealer(Guid id)
        {
            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == id);
            if (dealer == null) return NotFound("Dealer not found");

            var cars = (await _unitOfWork.CarRepository.GetAllAsync(x => x.DealerId == id)).ToList();
            foreach (var car in cars)
            {
                await _s3Service.DeleteCarPhotosAsync(car.DealerId, car.Id);
            }

            await _s3Service.DeleteDealerPhotosAsync(id);

                var carIds = cars.Select(c => c.Id).ToList();
                var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => carIds.Contains(x.CarId))).ToList();
            if (photos.Any())
            {
                _unitOfWork.CarPhotoRepository.RemoveRange(photos);
            }

            if (cars.Any())
            {
                _unitOfWork.CarRepository.RemoveRange(cars);
            }

            _unitOfWork.DealerRepository.Remove(dealer);
            await _unitOfWork.SaveAsync();

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Dealer deleted successfully"
            });
        }

        [HttpDelete("Delete-Multiple")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> deleteMultipleDealers([FromBody] List<Guid> dealerIds)
        {
            if (dealerIds == null || !dealerIds.Any())
            {
                return BadRequest("No dealer IDs provided");
            }

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var dealerId in dealerIds)
            {
                var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == dealerId);
                if (dealer == null)
                {
                    errors.Add($"Dealer not found (ID: {dealerId})");
                    continue;
                }

                var cars = (await _unitOfWork.CarRepository.GetAllAsync(x => x.DealerId == dealerId)).ToList();
                foreach (var car in cars)
                {
                    await _s3Service.DeleteCarPhotosAsync(car.DealerId, car.Id);
                }

                await _s3Service.DeleteDealerPhotosAsync(dealerId);

            var carIds = cars.Select(c => c.Id).ToList();
            var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => carIds.Contains(x.CarId))).ToList();
                if (photos.Any()) _unitOfWork.CarPhotoRepository.RemoveRange(photos);
                if (cars.Any()) _unitOfWork.CarRepository.RemoveRange(cars);

                _unitOfWork.DealerRepository.Remove(dealer);
                deletedCount++;
            }

            await _unitOfWork.SaveAsync();

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = $"{deletedCount} dealer(s) deleted successfully"
            });
        }
    }
}

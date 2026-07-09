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
    public class CarController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<User_data> _userManager;
        private readonly IS3Service _s3Service;

        public CarController(IUnitOfWork unitOfWork, UserManager<User_data> userManager, IS3Service s3Service)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _s3Service = s3Service;
        }

        private async Task<CarResponseDTO> MapCarToDto(Car car)
        {
            var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => x.CarId == car.Id)).OrderBy(p => p.Order).ToList();

            var dto = new CarResponseDTO
            {
                Id = car.Id,
                DealerId = car.DealerId,
                UserId = car.UserId,
                Title = car.Title,
                Price = car.Price,
                Year = car.Year,
                Make = car.Make,
                Model = car.Model,
                Trim = car.Trim,
                Mileage = car.Mileage,
                Transmission = car.Transmission,
                FuelType = car.FuelType,
                ExteriorColor = car.ExteriorColor,
                InteriorColor = car.InteriorColor,
                Vin = car.Vin,
                Description = car.Description,
                TitleType = car.TitleType,
                Status = car.Status,
                Featured = car.Featured,
                CreatedAt = car.CreatedAt,
                UpdatedAt = car.UpdatedAt,
                Photos = photos.Select(p => new CarPhotoDTO
                {
                    Id = p.Id,
                    CarId = p.CarId,
                    S3Key = p.S3Key,
                    S3Url = _s3Service.GetCarPhotoPublicUrl(p.S3Key),
                    FileName = p.FileName,
                    ContentType = p.ContentType,
                    FileSize = p.FileSize,
                    IsCover = p.IsCover,
                    Order = p.Order
                }).ToList()
            };

            return dto;
        }

        [HttpGet("All-Cars")]
        public async Task<ActionResult<IEnumerable<CarResponseDTO>>> getAllCars()
        {
            var cars = await _unitOfWork.CarRepository.GetAllAsync();
            if (cars == null || !cars.Any()) return Ok(new List<CarResponseDTO>());

            var carDtos = new List<CarResponseDTO>();

            foreach (var car in cars)
            {
                carDtos.Add(await MapCarToDto(car));
            }

            return Ok(carDtos);
        }

        [HttpGet("ByDealer/{dealerId}")]
        public async Task<ActionResult<IEnumerable<CarResponseDTO>>> getCarsByDealer(Guid dealerId)
        {
            var cars = await _unitOfWork.CarRepository.GetAllAsync(x => x.DealerId == dealerId);
            if (cars == null || !cars.Any()) return BadRequest("Cars not found");

            var carDtos = new List<CarResponseDTO>();

            foreach (var car in cars)
            {
                carDtos.Add(await MapCarToDto(car));
            }

            return Ok(carDtos);
        }

        [HttpGet("ById/{id}")]
        public async Task<ActionResult<CarResponseDTO>> getCarById(int id)
        {
            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == id);
            if (car == null) return NotFound("Car not found");

            return Ok(await MapCarToDto(car));
        }

        [HttpPost("Upsert")]
        public async Task<ActionResult<AuthResponseDTO>> upsertCar([FromBody] CarDTO upsertDto)
        {
            try
            {
                // Validar que el dealer existe
                var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == upsertDto.DealerId);
                if (dealer == null) return NotFound("Dealer not found");

                // Validar que el dealer está activo
                if (!dealer.IsActive) return BadRequest("Dealer is not active");

                // 🔐 ID del usuario logueado desde el JWT
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized("Invalid token");

                // Validar que el usuario pertenece al dealer
                if (dealer.OwnerId != currentUserId)
                {
                    var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
                    if (!roles.Contains(Roles.Role_Admin))
                    {
                        return Unauthorized("You do not have permission to modify cars for this dealer");
                    }
                }

                // ✅ MODO UPDATE - Si trae ID
                if (upsertDto.Id.HasValue)
                {
                    var existingCar = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == upsertDto.Id.Value);
                    if (existingCar == null) return NotFound("Car not found");

                    // Validar que el carro pertenece al mismo dealer
                    if (existingCar.DealerId != upsertDto.DealerId)
                    {
                        return BadRequest("Car does not belong to this dealer");
                    }

                    // Actualizar campos
                    existingCar.Title = upsertDto.Title;
                    existingCar.Price = upsertDto.Price;
                    existingCar.Year = upsertDto.Year;
                    existingCar.Make = upsertDto.Make;
                    existingCar.Model = upsertDto.Model;
                    existingCar.Trim = upsertDto.Trim;
                    existingCar.Mileage = upsertDto.Mileage;
                    existingCar.Transmission = upsertDto.Transmission;
                    existingCar.FuelType = upsertDto.FuelType;
                    existingCar.ExteriorColor = upsertDto.ExteriorColor;
                    existingCar.InteriorColor = upsertDto.InteriorColor;
                    existingCar.Vin = upsertDto.Vin;
                    existingCar.Description = upsertDto.Description;
                    existingCar.TitleType = upsertDto.TitleType ?? "Clean";
                    existingCar.Status = upsertDto.Status;
                    existingCar.Featured = upsertDto.Featured;
                    existingCar.UpdatedAt = DateTime.UtcNow;
                    existingCar.UserId = currentUserId;

                    _unitOfWork.CarRepository.Update(existingCar);
                    await _unitOfWork.SaveAsync();

                    return Ok(new AuthResponseDTO
                    {
                        Success = true,
                        Message = "Car updated successfully"
                    });

                }
                // ✅ MODO CREATE - Si NO trae ID
                else
                {
                    var newCar = new Car
                    {
                        DealerId = upsertDto.DealerId,
                        UserId = currentUserId,
                        Title = upsertDto.Title,
                        Price = upsertDto.Price,
                        Year = upsertDto.Year,
                        Make = upsertDto.Make,
                        Model = upsertDto.Model,
                        Trim = upsertDto.Trim,
                        Mileage = upsertDto.Mileage,
                        Transmission = upsertDto.Transmission,
                        FuelType = upsertDto.FuelType,
                        ExteriorColor = upsertDto.ExteriorColor,
                        InteriorColor = upsertDto.InteriorColor,
                        Vin = upsertDto.Vin,
                        Description = upsertDto.Description,
                        TitleType = upsertDto.TitleType ?? "Clean",
                        Status = upsertDto.Status,
                        Featured = upsertDto.Featured,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _unitOfWork.CarRepository.Add(newCar);
                    await _unitOfWork.SaveAsync();

                    return Ok(new AuthResponseDTO
                    {
                        Success = true,
                        Message = "Car created successfully",
                        Data = new { carId = newCar.Id }
                    });

                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred", error = ex.Message });
            }
        }

        [HttpPost("photos/presigned/{carId}")]
        public async Task<IActionResult> GetCarPhotoPresignedUrl(int carId, [FromBody] CarPhotoUploadRequest request)
        {
            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == carId);
            if (car == null) return NotFound("Car not found");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("Invalid token");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == car.DealerId);
            if (dealer == null || (dealer.OwnerId != currentUserId))
            {
                var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
                if (!roles.Contains(Roles.Role_Admin))
                    return Unauthorized("You do not have permission");
            }

            try
            {
                var result = await _s3Service.GenerateCarPhotoPresignedUrlAsync(
                    car.DealerId, dealer.Name, carId, request.FileName, request.ContentType);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { message = "Failed to generate upload URL", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred", error = ex.Message });
            }
        }

        [HttpPost("photos/register/{carId}")]
        public async Task<ActionResult<CarPhotoDTO>> RegisterCarPhoto(int carId, [FromBody] RegisterCarPhotoRequest request)
        {
            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == carId);
            if (car == null) return NotFound("Car not found");

            var photoCount = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => x.CarId == carId)).Count();

            var photo = new CarPhoto
            {
                CarId = carId,
                S3Key = request.S3Key,
                S3Url = _s3Service.GetCarPhotoPublicUrl(request.S3Key),
                FileName = request.FileName,
                ContentType = request.ContentType,
                FileSize = request.FileSize,
                IsCover = request.IsCover,
                Order = request.Order >= 0 ? request.Order : photoCount
            };

            if (photo.IsCover)
            {
                var existingCover = await _unitOfWork.CarPhotoRepository.GetFirstOrDefaultAsync(x => x.CarId == carId && x.IsCover);
                if (existingCover != null)
                {
                    existingCover.IsCover = false;
                    _unitOfWork.CarPhotoRepository.Update(existingCover);
                }
            }

            _unitOfWork.CarPhotoRepository.Add(photo);
            await _unitOfWork.SaveAsync();

            return Ok(new CarPhotoDTO
            {
                Id = photo.Id,
                CarId = photo.CarId,
                S3Key = photo.S3Key,
                S3Url = photo.S3Url,
                FileName = photo.FileName,
                ContentType = photo.ContentType,
                FileSize = photo.FileSize,
                IsCover = photo.IsCover,
                Order = photo.Order
            });
        }

        [HttpDelete("photos/{photoId}")]
        public async Task<ActionResult> DeleteCarPhoto(int photoId)
        {
            var photo = await _unitOfWork.CarPhotoRepository.GetFirstOrDefaultAsync(x => x.Id == photoId);
            if (photo == null) return NotFound("Photo not found");

            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == photo.CarId);
            if (car == null) return NotFound("Car not found");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("Invalid token");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == car.DealerId);
            if (dealer == null || (dealer.OwnerId != currentUserId))
            {
                var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
                if (!roles.Contains(Roles.Role_Admin))
                    return Unauthorized("You do not have permission");
            }

            // Delete from S3
            await _s3Service.DeleteObjectAsync(photo.S3Key);

            // Delete from DB
            _unitOfWork.CarPhotoRepository.Remove(photo);
            await _unitOfWork.SaveAsync();

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Photo deleted successfully"
            });
        }

        [HttpDelete("Delete/{id}")]
        public async Task<ActionResult> deleteCar(int id)
        {
            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == id);
            if (car == null) return NotFound("Car not found");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("Invalid token");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == car.DealerId);
            if (dealer == null) return NotFound("Dealer not found");

            if (dealer.OwnerId != currentUserId)
            {
                var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
                if (!roles.Contains(Roles.Role_Admin))
                {
                    return Unauthorized("You do not have permission to delete this car");
                }
            }

            await _s3Service.DeleteCarPhotosAsync(car.DealerId, id);

            var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => x.CarId == id)).ToList();
            if (photos.Any())
            {
                _unitOfWork.CarPhotoRepository.RemoveRange(photos);
            }

            _unitOfWork.CarRepository.Remove(car);
            await _unitOfWork.SaveAsync();

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = "Car deleted successfully"
            });

        }

        [HttpDelete("Delete-Multiple")]
        public async Task<ActionResult> deleteMultipleCars([FromBody] List<int> carIds)
        {
            if (carIds == null || !carIds.Any())
            {
                return BadRequest("No car IDs provided");
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("Invalid token");

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var carId in carIds)
            {
                var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(x => x.Id == carId);
                if (car == null)
                {
                    errors.Add($"Car not found (ID: {carId})");
                    continue;
                }

                var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(x => x.Id == car.DealerId);
                if (dealer == null || (dealer.OwnerId != currentUserId))
                {
                    var roles = await _userManager.GetRolesAsync(await _userManager.FindByIdAsync(currentUserId));
                    if (!roles.Contains(Roles.Role_Admin))
                    {
                        errors.Add($"Unauthorized to delete car (ID: {carId})");
                        continue;
                    }
                }

                await _s3Service.DeleteCarPhotosAsync(car.DealerId, carId);

                var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => x.CarId == carId)).ToList();
                if (photos.Any())
                {
                    _unitOfWork.CarPhotoRepository.RemoveRange(photos);
                }

                _unitOfWork.CarRepository.Remove(car);
                deletedCount++;
            }

            await _unitOfWork.SaveAsync();

            return Ok(new AuthResponseDTO
            {
                Success = true,
                Message = $"{deletedCount} car(s) deleted successfully"
            });
        }
    }

    public class CarPhotoUploadRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "image/jpeg";
    }

    public class RegisterCarPhotoRequest
    {
        public string S3Key { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSize { get; set; }
        public bool IsCover { get; set; }
        public int Order { get; set; } = -1;
    }
}

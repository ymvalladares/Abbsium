using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.ModelDTO;
using Server.Repositories.IRepositories;
using Server.Services;

namespace Server.Controllers
{
    [AllowAnonymous]
    public class PublicCarController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IS3Service _s3Service;

        public PublicCarController(IUnitOfWork unitOfWork, IS3Service s3Service)
        {
            _unitOfWork = unitOfWork;
            _s3Service = s3Service;
        }

        private async Task<PublicCarResponseDTO> MapCarToPublicDto(Car car)
        {
            var photos = (await _unitOfWork.CarPhotoRepository.GetAllAsync(x => x.CarId == car.Id))
                .OrderBy(p => p.Order)
                .ToList();

            return new PublicCarResponseDTO
            {
                Id = car.Id,
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
                Description = car.Description,
                TitleType = car.TitleType,
                Status = car.Status,
                Featured = car.Featured,
                CreatedAt = car.CreatedAt,
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
        }

        [HttpGet("dealer/{domain}/cars")]
        public async Task<ActionResult<IEnumerable<PublicCarResponseDTO>>> GetDealerCars(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest("Domain is required");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(
                x => x.Domain.ToLower() == domain.ToLower());

            if (dealer == null)
                return NotFound("Dealer not found");

            if (!dealer.IsActive)
                return NotFound("Dealer is not active");

            var cars = await _unitOfWork.CarRepository.GetAllAsync(
                x => x.DealerId == dealer.Id && x.Status == "available");

            if (cars == null || !cars.Any())
                return Ok(new List<PublicCarResponseDTO>());

            var carDtos = new List<PublicCarResponseDTO>();
            foreach (var car in cars)
            {
                carDtos.Add(await MapCarToPublicDto(car));
            }

            return Ok(carDtos);
        }

        [HttpGet("dealer/{domain}/cars/{id}")]
        public async Task<ActionResult<PublicCarResponseDTO>> GetDealerCarById(string domain, int id)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest("Domain is required");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(
                x => x.Domain.ToLower() == domain.ToLower());

            if (dealer == null)
                return NotFound("Dealer not found");

            if (!dealer.IsActive)
                return NotFound("Dealer is not active");

            var car = await _unitOfWork.CarRepository.GetFirstOrDefaultAsync(
                x => x.Id == id && x.DealerId == dealer.Id);

            if (car == null)
                return NotFound("Car not found");

            return Ok(await MapCarToPublicDto(car));
        }

        [HttpGet("dealer/{domain}/info")]
        public async Task<ActionResult<PublicDealerInfoDTO>> GetDealerInfo(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest("Domain is required");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(
                x => x.Domain.ToLower() == domain.ToLower());

            if (dealer == null)
                return NotFound("Dealer not found");

            if (!dealer.IsActive)
                return NotFound("Dealer is not active");

            return Ok(new PublicDealerInfoDTO
            {
                Name = dealer.Name,
                Logo = dealer.Logo,
                PrimaryColor = dealer.PrimaryColor
            });
        }

        [HttpGet("dealer/{domain}/featured-cars")]
        public async Task<ActionResult<IEnumerable<PublicCarResponseDTO>>> GetDealerFeaturedCars(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return BadRequest("Domain is required");

            var dealer = await _unitOfWork.DealerRepository.GetFirstOrDefaultAsync(
                x => x.Domain.ToLower() == domain.ToLower());

            if (dealer == null)
                return NotFound("Dealer not found");

            if (!dealer.IsActive)
                return NotFound("Dealer is not active");

            var cars = await _unitOfWork.CarRepository.GetAllAsync(
                x => x.DealerId == dealer.Id && x.Status == "available" && x.Featured);

            if (cars == null || !cars.Any())
                return Ok(new List<PublicCarResponseDTO>());

            var carDtos = new List<PublicCarResponseDTO>();
            foreach (var car in cars)
            {
                carDtos.Add(await MapCarToPublicDto(car));
            }

            return Ok(carDtos);
        }
    }
}

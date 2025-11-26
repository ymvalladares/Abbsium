using AutoMapper;
using Microsoft.AspNetCore.Authorization;
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

        public UserController(IUnitOfWork unitOfWork, IMapper mapper )
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserModelDto>>> getAllUser()
        {
            var query =  _unitOfWork.UserRepository.GetAll();
            if (query == null || !query.Any()) return BadRequest("Users not found");

            var mappedUsers = _mapper.Map<IEnumerable<UserModelDto>>(query);
            return Ok(mappedUsers);
        }



        [HttpGet("ById/{id}")]
        public async Task<ActionResult<UserModelDto>> getUserById(string id)
        {
            var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.Id == id);
            if (user == null) return NotFound("User not found");

            var dto = _mapper.Map<UserModelDto>(user);
            return Ok(dto);
        }

        [HttpGet("ByUserName/{userName}")]
        public async Task<ActionResult<UserModelDto>> getUserByUserName(string userName)
        {
            var user = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.UserName == userName);
            if (user == null) return NotFound("User not found");

            var dto = _mapper.Map<UserModelDto>(user);
            return Ok(dto);
        }
    }
}

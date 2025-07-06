using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Entitys;
using Server.Repositories.IRepositories;

namespace Server.Controllers
{
    [Authorize]
    public class UserController : Base_Control_Api
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User_data>>> getAllUser()
        {
            var query = _unitOfWork.UserRepository.GetAll();
            if (query == null) return BadRequest("Users not found");
            return Ok(query);
        }



        [HttpGet("ById/{id}")]
        public async Task<ActionResult<User_data>> getUserById(string id)
        {
            var query = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.Id == id);
            return Ok(query);
        }

        [HttpGet("ByUserName/{userName}")]
        public async Task<ActionResult<User_data>> getAllUser(string userName)
        {
            var query = _unitOfWork.UserRepository.GetFirstOrDefault(x => x.UserName == userName);
            return Ok(query);
        }
    }
}

using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Server.Entitys;
using Server.ModelDTO;

namespace Server.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<User_data, UserModelDto>();
        }
    }
}

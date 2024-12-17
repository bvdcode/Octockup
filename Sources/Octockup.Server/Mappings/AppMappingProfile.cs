using AutoMapper;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Mappings
{
    public class AppMappingProfile : Profile
    {
        public AppMappingProfile()
        {
            CreateMap<User, UserDto>();
        }
    }
}

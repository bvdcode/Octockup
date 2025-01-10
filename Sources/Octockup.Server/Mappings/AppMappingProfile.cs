using AutoMapper;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Mappings
{
    public class AppMappingProfile : Profile
    {
        public AppMappingProfile()
        {
            CreateMap<User, UserDto>();
            CreateMap<BackupTask, BackupTaskDto>();
            CreateMap<BackupSnapshot, BackupSnapshotDto>()
                .ForMember(dest => dest.TotalSizeFormatted, opt => opt
                .MapFrom(src => FileSystemHelpers.FormatSize(src.TotalSize)));
        }
    }
}

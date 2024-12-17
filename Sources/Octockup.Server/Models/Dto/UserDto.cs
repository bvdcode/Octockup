using Octockup.Server.Database.Enums;

namespace Octockup.Server.Models.Dto
{
    public record UserDto(int Id, string Username, string Email, UserRole Role);
}

using Octockup.Server.Models.Dto;

namespace Octockup.Server.Models
{
    public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);
}

using Octockup.Server.Models.Dto;

namespace Octockup.Server.Models
{
    public class AuthResponse
    {
        public UserDto User { get; set; } = null!;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
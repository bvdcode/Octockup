using Octockup.Server.Database.Enums;

namespace Octockup.Server.Models.Dto
{
    public class UserDto : BaseDto
    {
        public UserRole Role { get; set; }
        public long? StorageLimitBytes { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}

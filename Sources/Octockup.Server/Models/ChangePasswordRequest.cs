using MediatR;

namespace Octockup.Server.Models
{
    public class ChangePasswordRequest : IRequest
    {
        public string Username { get; set; } = string.Empty;
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
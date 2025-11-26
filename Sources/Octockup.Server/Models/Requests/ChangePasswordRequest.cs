using MediatR;

namespace Octockup.Server.Models.Requests
{
    public class ChangePasswordRequest : IRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
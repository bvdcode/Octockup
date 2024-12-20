using MediatR;

namespace Octockup.Server.Models
{
    public class ChangePasswordRequest : IRequest
    {
        public int UserId { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
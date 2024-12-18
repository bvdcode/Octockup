using MediatR;

namespace Octockup.Server.Models
{
    public record ChangePasswordRequest(string NewPassword, int UserId) : IRequest;
}

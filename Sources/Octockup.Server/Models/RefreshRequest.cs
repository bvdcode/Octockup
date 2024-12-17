using MediatR;

namespace Octockup.Server.Models
{
    public record RefreshRequest(string RefreshToken) : IRequest<AuthResponse>;
}

using MediatR;

namespace Octockup.Server.Models
{
    public record LoginRequest(string Username, string PasswordHash) : IRequest<AuthResponse>;
}

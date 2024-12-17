using MediatR;
using Octockup.Server.Database;

namespace Octockup.Server.Models
{
    public record CreateTokenRequest(User User) : IRequest<AuthResponse>;
}

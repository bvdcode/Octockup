using MediatR;
using Octockup.Server.Database;

namespace Octockup.Server.Models
{
    public class CreateTokenRequest : IRequest<AuthResponse>
    {
        public User User { get; set; } = null!;
    }
}
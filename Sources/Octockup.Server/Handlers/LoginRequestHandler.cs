using MediatR;
using Octockup.Server.Models;

namespace Octockup.Server.Handlers
{
    public class LoginRequestHandler : IRequestHandler<LoginRequest, AuthResponse>
    {
        public Task<AuthResponse> Handle(LoginRequest request, CancellationToken cancellationToken)
        {

        }
    }
}

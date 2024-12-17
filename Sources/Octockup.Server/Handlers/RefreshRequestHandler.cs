using MediatR;
using Octockup.Server.Models;

namespace Octockup.Server.Handlers
{
    public class RefreshRequestHandler : IRequestHandler<RefreshRequest, AuthResponse>
    {
        public Task<AuthResponse> Handle(RefreshRequest request, CancellationToken cancellationToken)
        {

        }
    }
}

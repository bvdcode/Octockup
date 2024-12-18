using EasyExtensions.Extensions;
using MediatR;
using Octockup.Server.Models;

namespace Octockup.Server.Handlers
{
    public class ChangePasswordRequestHandler : IRequestHandler<ChangePasswordRequest>
    {
        public Task Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            string hash = request.NewPassword.SHA512();

        }
    }
}

using MediatR;
using System.Net;
using EasyExtensions;
using Octockup.Server.Models;
using Octockup.Server.Database;
using EasyExtensions.Extensions;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Octockup.Server.Handlers
{
    public class ChangePasswordRequestHandler(AppDbContext _dbContext) 
        : IRequestHandler<ChangePasswordRequest>
    {
        public Task Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            HttpContextAccessor _accessor = null!;
            string hash = request.NewPassword.SHA512();
            ArgumentNullException.ThrowIfNull(_accessor.HttpContext);
            int userId = _accessor.HttpContext.User.GetId();
            var foundUser = _dbContext.Users.Find(userId) 
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(User), "User not found.");
            foundUser.PasswordHash = hash;
            _dbContext.Users.Update(foundUser);
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

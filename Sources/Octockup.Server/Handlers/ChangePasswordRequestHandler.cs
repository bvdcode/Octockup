using MediatR;
using System.Net;
using EasyExtensions;
using Octockup.Server.Models;
using Octockup.Server.Database;
using EasyExtensions.Abstractions;
using EasyExtensions.EntityFrameworkCore.Exceptions;

namespace Octockup.Server.Handlers
{
    public class ChangePasswordRequestHandler(
        AppDbContext _dbContext,
        IHttpContextAccessor _accessor,
        IPasswordHashService _passwords,
        ILogger<ChangePasswordRequestHandler> _logger) 
        : IRequestHandler<ChangePasswordRequest>
    {
        public async Task Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPassword, nameof(request.NewPassword));
            string phc = _passwords.Hash(request.NewPassword);
            ArgumentNullException.ThrowIfNull(_accessor.HttpContext);
            int userId = _accessor.HttpContext.User.GetId();
            var foundUser = _dbContext.Users.Find(userId) 
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(User), "User not found.");
            foundUser.PasswordPhc = phc;
            _logger.LogInformation("User {user} changed password.", foundUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

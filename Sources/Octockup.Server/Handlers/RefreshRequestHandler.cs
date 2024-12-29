using MediatR;
using System.Net;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Exceptions;
using EasyExtensions.AspNetCore.Authorization.Services;

namespace Octockup.Server.Handlers
{
    public class RefreshRequestHandler(ITokenProvider _tokenProvider, IMediator _mediator,
        AppDbContext _dbContext) : IRequestHandler<RefreshRequest, AuthResponse>
    {
        public async Task<AuthResponse> Handle(RefreshRequest request, CancellationToken cancellationToken)
        {
            bool isValid = _tokenProvider.ValidateToken(request.RefreshToken);
            if (!isValid)
            {
                throw new WebApiException(HttpStatusCode.Unauthorized, nameof(Session), "Invalid refresh token");
            }
            var foundToken = _dbContext.Sessions
                .Include(x => x.User)
                .FirstOrDefault(x => x.RefreshToken == request.RefreshToken)
                ?? throw new WebApiException(HttpStatusCode.NotFound, nameof(Session), "Session not found");
            _dbContext.Sessions.Update(foundToken);
            _dbContext.Sessions.Remove(foundToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            CreateTokenRequest createTokenRequest = new() { User = foundToken.User };
            return await _mediator.Send(createTokenRequest, cancellationToken);
        }
    }
}

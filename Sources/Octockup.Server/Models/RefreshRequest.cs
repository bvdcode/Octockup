using MediatR;

namespace Octockup.Server.Models
{
    public class RefreshRequest : IRequest<AuthResponse>
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
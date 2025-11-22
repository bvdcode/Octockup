using MediatR;

namespace Octockup.Server.Models
{
    public class LoginRequest : IRequest<AuthResponse>
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
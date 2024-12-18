using FluentValidation;
using Octockup.Server.Models;

namespace Octockup.Server.Validators
{
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .MinimumLength(1);
            RuleFor(x => x.PasswordHash)
                .NotEmpty()
                .Length(128);
        }
    }
}

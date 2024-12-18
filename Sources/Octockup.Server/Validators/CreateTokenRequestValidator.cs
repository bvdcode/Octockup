using FluentValidation;
using Octockup.Server.Models;

namespace Octockup.Server.Validators
{
    public class CreateTokenRequestValidator : AbstractValidator<CreateTokenRequest>
    {
        public CreateTokenRequestValidator()
        {
            RuleFor(x => x.User)
                .NotNull();
        }
    }
}

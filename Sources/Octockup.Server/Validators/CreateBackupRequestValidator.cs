using FluentValidation;
using Octockup.Server.Models;

namespace Octockup.Server.Validators
{
    public class CreateBackupRequestValidator : AbstractValidator<CreateBackupRequest>
    {
        public CreateBackupRequestValidator()
        {
            RuleFor(x => x.BackupName)
                .NotEmpty()
                .MaximumLength(255)
                .Matches("^[^\\n]*$");
            RuleFor(x => x.BackupName)
                .Matches("^[^\\n]*$");
            RuleFor(x => x.Provider)
                .NotEmpty();
            RuleFor(x => x.Interval)
                .InclusiveBetween(0, 31622400); // 1 year
            RuleFor(x => x.StartAt)
                .GreaterThanOrEqualTo(DateTime.UtcNow)
                .When(x => x.StartAt != default);
        }
    }
}

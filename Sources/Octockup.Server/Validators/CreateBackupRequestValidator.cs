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
        }
    }
}

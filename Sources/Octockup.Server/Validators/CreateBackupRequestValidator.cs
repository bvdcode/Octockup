using System.Net;
using FluentValidation;
using Octockup.Server.Models;

namespace Octockup.Server.Validators
{
    public class CreateBackupRequestValidator : AbstractValidator<CreateBackupRequest>
    {
        public CreateBackupRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(255)
                .Matches("^[^\\n]*$");
            RuleFor(x => x.Name)
                .Matches("^[^\\n]*$");
            RuleFor(x => x.Provider)
                .NotEmpty();
            RuleFor(x => x.Interval)
                .InclusiveBetween(0, 31622400); // 1 year
            RuleFor(x => x.StartAt)
                .GreaterThanOrEqualTo(DateTime.UtcNow)
                .When(x => x.StartAt != default);
            RuleFor(x => x.Parameters)
                // use custom validator and error message from it
                .Must((request, parameters) => ValidateParameters(parameters, out string error))
                .WithMessage(request => ValidateParameters(request.Parameters, out string error)
                ? throw new Exception("Unexpected error in CreateBackupRequestValidator")
                : error);
        }

        private static bool ValidateParameters(Dictionary<string, string> dictionary, out string error)
        {
            error = string.Empty;
            if (dictionary.TryGetValue("RemotePort", out var port))
            {
                if (!int.TryParse(port, out var portNumber) || portNumber < 0 || portNumber > 65535)
                {
                    error = "RemotePort is not a valid port number";
                    return false;
                }
            }
            if (dictionary.TryGetValue("RemoteHost", out var path))
            {
                if (Uri.CheckHostName(path) == UriHostNameType.Dns)
                {
                    try
                    {
                        var ip = Dns.GetHostAddresses(path);
                        if (ip.Length == 0)
                        {
                            error = "RemoteHost is not a valid domain name";
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = $"RemoteHost is not a valid domain name: {ex.Message}";
                        return false;
                    }
                }
                if (!IPAddress.TryParse(path, out _))
                {
                    error = "RemoteHost is not a valid IP address";
                    return false;
                }
            }

            return true;
        }
    }
}

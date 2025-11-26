using MediatR;

namespace Octockup.Server.Models.Requests
{
    public class SaveModuleRequest : IRequest
    {
        public string Tag { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}

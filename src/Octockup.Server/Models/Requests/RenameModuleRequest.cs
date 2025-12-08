namespace Octockup.Server.Models.Requests
{
    public record RenameModuleRequest
    {
        public required string NewTag { get; init; }
    }
}

namespace Octockup.Server.Abstractions
{
    public interface IBackupModule
    {
        string Id { get; }
        string Name { get; }
        IEnumerable<string> RequiredParameters { get; }
    }
}

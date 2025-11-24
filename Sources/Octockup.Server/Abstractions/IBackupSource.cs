namespace Octockup.Server.Abstractions
{
    public interface IBackupSource
    {
        string Id { get; }
        string Name { get; }
        IEnumerable<string> RequiredParameters { get; }
    }
}

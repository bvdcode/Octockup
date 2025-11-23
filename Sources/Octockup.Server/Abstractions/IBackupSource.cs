namespace Octockup.Server.Abstractions
{
    public interface IBackupSource
    {
        string Name { get; }
        IEnumerable<string> RequiredParameters { get; }
    }
}

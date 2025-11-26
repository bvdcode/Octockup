namespace Octockup.Server.Abstractions
{
    public interface IBackupModule
    {
        string Id { get; }
        string Name { get; }
        char PathSeparator { get; }
        IEnumerable<string> RequiredParameters { get; }
        void SetParameters(Dictionary<string, string> parameters);
    }
}

namespace Octockup.Server.Models
{
    public class StorageProviderInfo
    {
        public string Name { get; set; } = string.Empty;
        public IEnumerable<string> Parameters { get; set; } = [];
    }
}
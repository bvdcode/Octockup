namespace Octockup.Server.Models.Dto
{
    public class StorageStatsDto : ModuleDto
    {
        public int TotalBackups { get; internal set; }
        public long TotalOriginalSize { get; internal set; }
        public long TotalStoredSize { get; internal set; }
        public int DeduplicatedChunks { get; internal set; }
    }
}
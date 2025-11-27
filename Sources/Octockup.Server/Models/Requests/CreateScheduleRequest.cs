namespace Octockup.Server.Models.Requests
{
    public class CreateScheduleRequest
    {
        public Guid BackupId { get; set; }
        public DateTime StartAt { get; set; }
        public int? IntervalMinutes { get; set; }
    }
}

using Quartz;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 5)]
    public class BackupJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IScheduledService
    {
        IEnumerable<(Func<Task>, string)> GetScheduledJobs();
    }
}

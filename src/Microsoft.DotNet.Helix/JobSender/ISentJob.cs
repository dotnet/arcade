using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.Helix.Client
{
    public interface ISentJob
    {
        string CorrelationId { get; }
        string ResultsContainerUri { get; }
        string ResultsContainerReadSAS { get; }

        Task<JobPassFail> WaitAsync(int pollingIntervalMs = 10000, CancellationToken cancellationToken = default);
    }
}

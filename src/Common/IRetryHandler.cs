using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.VersionTools.Util
{
    public interface IRetryHandler
    {
        Task<bool> RunAsync(
            Func<int, Task<bool>> actionSuccessfulAsync);

        Task<bool> RunAsync(
            Func<int, Task<bool>> actionSuccessfulAsync,
            CancellationToken cancellationToken);
    }
}

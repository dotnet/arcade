using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.VersionTools.Util;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles
{
    public class MockRetryHandler : IRetryHandler
    {
        private readonly int _maxAttempts;

        public int ActualAttempts { get; private set; }

        public MockRetryHandler()
            : this(maxAttempts: 1)
        {
        }

        public MockRetryHandler(int maxAttempts)
        {
            _maxAttempts = maxAttempts;
        }

        public Task<bool> RunAsync(Func<int, Task<bool>> actionSuccessfulAsync)
            => RunAsync(actionSuccessfulAsync, CancellationToken.None);

        public async Task<bool> RunAsync(Func<int, Task<bool>> actionSuccessfulAsync, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < _maxAttempts; attempt++)
            {
                ActualAttempts++;

                var succeeded = await actionSuccessfulAsync(attempt);
                if (succeeded)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

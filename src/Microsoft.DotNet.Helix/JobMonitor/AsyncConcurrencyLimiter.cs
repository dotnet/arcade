// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// Shares one concurrency budget across otherwise independent asynchronous operations.
    /// </summary>
    internal sealed class AsyncConcurrencyLimiter : IDisposable
    {
        private readonly SemaphoreSlim _slots;

        public AsyncConcurrencyLimiter(int parallelism)
        {
            if (parallelism <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(parallelism));
            }

            _slots = new SemaphoreSlim(parallelism, parallelism);
        }

        public int AvailableSlots => _slots.CurrentCount;

        public async Task<T> RunAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);

            // The limiter is shared by callers that may already run inside separate job-level
            // pipelines, so this wait enforces one global budget rather than multiplying limits.
            await _slots.WaitAsync(cancellationToken);
            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                _slots.Release();
            }
        }

        public void Dispose() => _slots.Dispose();
    }
}

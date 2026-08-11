// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>Centralizes bounded retry of idempotent service reads and retry-safe writes.</summary>
    internal sealed class RetryExecutor
    {
        private readonly ILogger _logger;

        public RetryExecutor(ILogger logger) => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationDescription,
            CancellationToken cancellationToken,
            int maxAttempts = 5)
        {
            Exception lastTransientFailure = null;
            T result = default;

            // Arcade owns delay calculation and cancellation-aware waiting. This wrapper supplies
            // the monitor's transient classification and preserves the final causal exception.
            var retry = new ExponentialRetry
            {
                MaxAttempts = maxAttempts,
                DelayBase = 2,
                DelayConstant = 0,
                MinRandomFactor = 1,
                MaxRandomFactor = 1,
                RetryDelayCallback = (attempt, delay) => _logger.LogDebug(
                    "Transient failure while attempting to {Operation}. Waiting {Delay} before retry {Attempt} of {AttemptCount}.",
                    operationDescription, delay, attempt + 1, maxAttempts),
            };

            bool succeeded = await retry.RunAsync(async _ =>
            {
                try
                {
                    result = await operation(cancellationToken);
                    return RetryResult.Success;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested && TransientFailureDetector.IsTransient(ex))
                {
                    // Permanent exceptions deliberately escape this callback immediately. A caller
                    // must opt into this executor only when replaying the operation is safe.
                    lastTransientFailure = ex;
                    return RetryResult.Retry();
                }
            }, cancellationToken);

            return succeeded
                ? result
                : throw lastTransientFailure ?? new InvalidOperationException($"Retry for '{operationDescription}' ended unexpectedly.");
        }
    }
}

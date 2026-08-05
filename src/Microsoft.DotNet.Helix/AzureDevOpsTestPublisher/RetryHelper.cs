// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

public class RetryHelper
{
    public static async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        Exception? last = null;
        T result = default!;
        IRetryHandler retryHandler = new ExponentialRetry
        {
            MaxAttempts = 5,
            DelayBase = 2,
            DelayConstant = 0,
            MinRandomFactor = 1,
            MaxRandomFactor = 1,
        };

        bool succeeded = await retryHandler.RunAsync(
            async attempt =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    result = await action();
                    return RetryResult.Success;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < 4)
                {
                    last = ex;
                    return RetryResult.Retry();
                }
                catch (Exception ex)
                {
                    last = ex;
                    throw;
                }
            },
            cancellationToken);

        return succeeded
            ? result
            : throw last ?? new InvalidOperationException("Retry failed without capturing an exception.");
    }
}

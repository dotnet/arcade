// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

public class RetryHelper
{
    public static async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
        => await RetryAsync(
            action,
            retryCount: 4,
            static _ => true,
            onRetry: null,
            cancellationToken);

    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        int retryCount,
        Func<Exception, bool> isRetryable,
        Action<Exception, int>? onRetry,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);
        delayAsync ??= Task.Delay;

        for (int retry = 0; ; retry++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (retry < retryCount && isRetryable(ex))
            {
                onRetry?.Invoke(ex, retry + 1);
                await delayAsync(TimeSpan.FromSeconds(Math.Pow(2, retry + 1)), cancellationToken);
            }
        }
    }
}

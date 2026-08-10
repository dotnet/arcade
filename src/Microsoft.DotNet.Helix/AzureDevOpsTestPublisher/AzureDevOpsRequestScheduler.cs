// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

/// <summary>
/// Bounds concurrent Azure DevOps result and attachment requests and coordinates service-directed delays.
/// </summary>
public sealed class AzureDevOpsRequestScheduler : IDisposable
{
    private static readonly TimeSpan s_defaultTooManyRequestsDelay = TimeSpan.FromSeconds(30);

    private readonly Channel<ScheduledRequest> _requests;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly Task[] _workers;
    private readonly ILogger _logger;
    private readonly TimeSpan _tooManyRequestsDelay;
    private readonly object _delayLock = new();
    private DateTimeOffset _delayUntil;
    private int _activeRequestCount;
    private int _disposed;

    public AzureDevOpsRequestScheduler(int maximumConcurrency, ILogger logger)
        : this(maximumConcurrency, logger, s_defaultTooManyRequestsDelay)
    {
    }

    internal AzureDevOpsRequestScheduler(
        int maximumConcurrency,
        ILogger logger,
        TimeSpan tooManyRequestsDelay)
    {
        if (maximumConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        if (tooManyRequestsDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(tooManyRequestsDelay));
        }

        MaximumConcurrency = maximumConcurrency;
        _logger = logger;
        _tooManyRequestsDelay = tooManyRequestsDelay;
        _requests = Channel.CreateBounded<ScheduledRequest>(new BoundedChannelOptions(maximumConcurrency * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = maximumConcurrency == 1,
            SingleWriter = false,
        });
        _workers = [.. Enumerable.Range(0, maximumConcurrency).Select(_ => Task.Run(ProcessRequestsAsync))];
    }

    public int MaximumConcurrency { get; }

    internal int ActiveRequestCount => Volatile.Read(ref _activeRequestCount);

    internal async Task<HttpResponseMessage> SendAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var request = new ScheduledRequest(sendAsync, cancellationToken);
        try
        {
            await _requests.Writer.WriteAsync(request, cancellationToken);
            return await request.Completion.Task.WaitAsync(cancellationToken);
        }
        catch (ChannelClosedException) when (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(AzureDevOpsRequestScheduler));
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task ProcessRequestsAsync()
    {
        try
        {
            await foreach (ScheduledRequest request in _requests.Reader.ReadAllAsync(_disposeCancellation.Token))
            {
                if (request.CancellationToken.IsCancellationRequested)
                {
                    request.Completion.TrySetCanceled(request.CancellationToken);
                    continue;
                }

                Interlocked.Increment(ref _activeRequestCount);
                try
                {
                    using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        request.CancellationToken,
                        _disposeCancellation.Token);

                    try
                    {
                        await WaitForAdmissionAsync(linkedCancellation.Token);
                        HttpResponseMessage response = await request.SendAsync(linkedCancellation.Token);
                        RecordDelay(response);
                        request.Completion.TrySetResult(response);
                    }
                    catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
                    {
                        request.Completion.TrySetCanceled(request.CancellationToken);
                    }
                    catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
                    {
                        request.Completion.TrySetException(
                            new ObjectDisposedException(nameof(AzureDevOpsRequestScheduler)));
                    }
                    catch (Exception ex)
                    {
                        request.Completion.TrySetException(ex);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref _activeRequestCount);
                }
            }
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
    }

    private async Task WaitForAdmissionAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            TimeSpan delay;
            lock (_delayLock)
            {
                delay = _delayUntil - DateTimeOffset.UtcNow;
            }

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    private void RecordDelay(HttpResponseMessage response)
    {
        TimeSpan? retryAfterDelay = GetRetryAfterDelay(response);
        TimeSpan delay = retryAfterDelay ?? TimeSpan.Zero;

        if (response.Headers.TryGetValues("X-RateLimit-Delay", out IEnumerable<string>? delayValues)
            && double.TryParse(delayValues.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double delaySeconds)
            && delaySeconds > 0)
        {
            delay = TimeSpan.FromSeconds(Math.Max(delay.TotalSeconds, delaySeconds));
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && retryAfterDelay is null)
        {
            delay = delay > _tooManyRequestsDelay ? delay : _tooManyRequestsDelay;
        }

        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        lock (_delayLock)
        {
            DateTimeOffset proposedDelayUntil = DateTimeOffset.UtcNow + delay;
            if (proposedDelayUntil > _delayUntil)
            {
                _delayUntil = proposedDelayUntil;
            }
        }

        _logger.LogDebug(
            "Azure DevOps requested a global result-upload delay of {DelaySeconds:0.###}s.",
            delay.TotalSeconds);
    }

    internal static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan delay = date - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return delay;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _requests.Writer.TryComplete();
        _disposeCancellation.Cancel();

        while (_requests.Reader.TryRead(out ScheduledRequest? request))
        {
            request.Completion.TrySetException(new ObjectDisposedException(nameof(AzureDevOpsRequestScheduler)));
        }

        Task.WhenAll(_workers).GetAwaiter().GetResult();

        while (_requests.Reader.TryRead(out ScheduledRequest? request))
        {
            request.Completion.TrySetException(new ObjectDisposedException(nameof(AzureDevOpsRequestScheduler)));
        }

        _disposeCancellation.Dispose();
    }

    private sealed record ScheduledRequest(
        Func<CancellationToken, Task<HttpResponseMessage>> SendAsync,
        CancellationToken CancellationToken)
    {
        public TaskCompletionSource<HttpResponseMessage> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.JobMonitor;

internal sealed class AzureDevOpsRateLimitGate
{
    private readonly object _sync = new();
    private readonly JobMonitorMetrics _metrics;
    private long _notBeforeUtcTicks;

    public AzureDevOpsRateLimitGate(JobMonitorMetrics metrics = null)
    {
        _metrics = metrics;
    }

    /// <summary>
    /// Extends the shared request deadline. A shared deadline coordinates all concurrent upload
    /// workers; independent delays would allow other workers to continue issuing throttled calls.
    /// </summary>
    public void ExtendDeadline(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        _metrics?.RecordRateLimitDeferral(delay);
        long candidate = DateTimeOffset.UtcNow.Add(delay).UtcTicks;
        lock (_sync)
        {
            _notBeforeUtcTicks = Math.Max(_notBeforeUtcTicks, candidate);
        }
    }

    /// <summary>Waits until the current shared request deadline has passed.</summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        long waitStartedAt = 0;
        try
        {
            // Re-read after every delay because another worker may extend the deadline while this
            // worker is waiting.
            while (true)
            {
                long notBeforeTicks;
                lock (_sync)
                {
                    notBeforeTicks = _notBeforeUtcTicks;
                }

                TimeSpan delay = TimeSpan.FromTicks(notBeforeTicks - DateTimeOffset.UtcNow.UtcTicks);
                if (delay <= TimeSpan.Zero)
                {
                    return;
                }

                waitStartedAt = waitStartedAt == 0 ? JobMonitorMetrics.StartOperation() : waitStartedAt;
                await Task.Delay(delay, cancellationToken);
            }
        }
        finally
        {
            if (waitStartedAt != 0)
            {
                _metrics?.RecordRateLimitWait(waitStartedAt);
            }
        }
    }
}

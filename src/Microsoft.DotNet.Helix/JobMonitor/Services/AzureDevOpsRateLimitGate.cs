// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.JobMonitor;

internal sealed class AzureDevOpsRateLimitGate
{
    private readonly JobMonitorMetrics _metrics;
    private long _notBeforeUtcTicks;

    public AzureDevOpsRateLimitGate(JobMonitorMetrics metrics = null)
    {
        _metrics = metrics;
    }

    public void Defer(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        _metrics?.RecordRateLimitDeferral(delay);
        long candidate = DateTimeOffset.UtcNow.Add(delay).UtcTicks;
        long observed;
        while (candidate > (observed = Interlocked.Read(ref _notBeforeUtcTicks)))
        {
            if (Interlocked.CompareExchange(ref _notBeforeUtcTicks, candidate, observed) == observed)
            {
                break;
            }
        }
    }

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        long waitStartedAt = 0;
        try
        {
            while (true)
            {
                long notBeforeTicks = Interlocked.Read(ref _notBeforeUtcTicks);
                TimeSpan delay = new DateTimeOffset(notBeforeTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
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

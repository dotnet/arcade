// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Helix.JobMonitor;

internal sealed class AzureDevOpsRateLimitGate
{
    private long _notBeforeUtcTicks;

    public void Defer(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

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
        while (true)
        {
            long notBeforeTicks = Interlocked.Read(ref _notBeforeUtcTicks);
            TimeSpan delay = new DateTimeOffset(notBeforeTicks, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }
}

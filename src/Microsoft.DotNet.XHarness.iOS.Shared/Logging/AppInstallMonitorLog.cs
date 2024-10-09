// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

// Monitor the output from 'mlaunch --installdev' and cancel the installation if there's no output for 1 minute.
public class AppInstallMonitorLog : FileBackedLog
{
    private readonly IFileBackedLog _copyTo;
    private readonly CancellationTokenSource _cancellationSource;

    public override string FullPath => _copyTo.FullPath;

    public CancellationToken CancellationToken => _cancellationSource.Token;

    public bool CopyingApp;
    public bool CopyingWatchApp;
    public TimeSpan AppCopyDuration;
    public TimeSpan WatchAppCopyDuration;
    public Stopwatch AppCopyStart = new();
    public Stopwatch WatchAppCopyStart = new();
    public int AppPercentComplete;
    public int WatchAppPercentComplete;
    public long AppBytes;
    public long WatchAppBytes;
    public long AppTotalBytes;
    public long WatchAppTotalBytes;

    public AppInstallMonitorLog(IFileBackedLog copy_to)
            : base($"Watch transfer log for {copy_to.Description}")
    {
        _copyTo = copy_to;
        _cancellationSource = new CancellationTokenSource();
        _cancellationSource.Token.Register(() =>
        {
            copy_to.WriteLine("App installation cancelled: it timed out after no output for 1 minute.");
        });
    }

    public override Encoding Encoding => _copyTo.Encoding;

    public override void Flush() => _copyTo.Flush();

    public override StreamReader GetReader() => _copyTo.GetReader();

    public override void Dispose()
    {
        _copyTo.Dispose();
        _cancellationSource.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ResetTimer() => _cancellationSource.CancelAfter(TimeSpan.FromMinutes(1));

    protected override void WriteImpl(string value)
    {
        var v = value.Trim();
        if (v.StartsWith("Installing application bundle", StringComparison.Ordinal))
        {
            if (!CopyingApp)
            {
                CopyingApp = true;
                AppCopyStart.Start();
            }
            else if (!CopyingWatchApp)
            {
                CopyingApp = false;
                CopyingWatchApp = true;
                AppCopyStart.Stop();
                WatchAppCopyStart.Start();
            }
        }
        else if (v.StartsWith("PercentComplete: ", StringComparison.Ordinal) && int.TryParse(v.Substring("PercentComplete: ".Length).Trim(), out var percent))
        {
            if (CopyingApp)
            {
                AppPercentComplete = percent;
            }
            else if (CopyingWatchApp)
            {
                WatchAppPercentComplete = percent;
            }
        }
        else if (v.StartsWith("NumBytes: ", StringComparison.Ordinal) && int.TryParse(v.Substring("NumBytes: ".Length).Trim(), out var num_bytes))
        {
            if (CopyingApp)
            {
                AppBytes = num_bytes;
                AppCopyDuration = AppCopyStart.Elapsed;
            }
            else if (CopyingWatchApp)
            {
                WatchAppBytes = num_bytes;
                WatchAppCopyDuration = WatchAppCopyStart.Elapsed;
            }
        }
        else if (v.StartsWith("TotalBytes: ", StringComparison.Ordinal) && int.TryParse(v.Substring("TotalBytes: ".Length).Trim(), out var total_bytes))
        {
            if (CopyingApp)
            {
                AppTotalBytes = total_bytes;
            }
            else if (CopyingWatchApp)
            {
                WatchAppTotalBytes = total_bytes;
            }
        }

        ResetTimer();

        _copyTo.WriteLine(value);
    }

    public override void Write(byte[] buffer, int offset, int count) => _copyTo.Write(buffer, offset, count);
}

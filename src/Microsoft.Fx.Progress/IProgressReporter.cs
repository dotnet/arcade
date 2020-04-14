// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Microsoft.Fx.Progress
{
    public interface IProgressReporter
    {
        void Cancel();

        bool IsRunning { get; }
        CancellationToken CancellationToken { get; }
        bool CanCancel { get; }
        string Task { get; }
        string Details { get; }
        float PercentageComplete { get; }
        bool IsIndeterminate { get; }
        TimeSpan RemainingTime { get; set; }

        event EventHandler IsRunningChanged;
        event EventHandler TaskChanged;
        event EventHandler DetailsChanged;
        event EventHandler PercentageCompleteChanged;
        event EventHandler IsIndeterminateChanged;
        event EventHandler RemainingTimeChanged;
    }
}

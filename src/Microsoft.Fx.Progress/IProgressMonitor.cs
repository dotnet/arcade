// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Microsoft.Fx.Progress
{
    public interface IProgressMonitor : IDisposable
    {
        void SetTask(string description);
        void SetDetails(string description);
        void SetRemainingWork(float totalUnits);
        void Report(float units);

        CancellationToken CancellationToken { get; }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Fx.Progress
{
    public interface IModalBackgroundRunner
    {
        void Run(Action<IProgressMonitor> action);
        void RunNoncancelable(Action<IProgressMonitor> action);

        T Run<T>(Func<IProgressMonitor, T> action);
        T RunNoncancelable<T>(Func<IProgressMonitor, T> action);
    }
}

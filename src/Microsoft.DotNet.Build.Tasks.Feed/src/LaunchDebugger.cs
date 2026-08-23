// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using  Microsoft.Build.Utilities;
using System.Diagnostics;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    // Not opted into multithreading, and deliberately never will be: Debugger.Launch attaches a
    // debugger to the whole process. In a shared multithreaded node that would affect every project
    // building on that node, and concurrent invocations would race to attach. Keeping this
    // diagnostic task in the out-of-proc TaskHost confines the attach to a single sidecar.
    public class LaunchDebugger : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            Debugger.Launch();
            return true;
        }
    }
}

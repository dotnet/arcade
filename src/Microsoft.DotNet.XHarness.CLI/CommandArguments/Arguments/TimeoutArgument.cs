// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// How long XHarness should wait until a test execution completes before clean up (kill running apps, uninstall, etc)
/// </summary>
internal class TimeoutArgument : TimeSpanArgument
{
    public TimeoutArgument(TimeSpan defaultTimeout)
        : base("timeout=", "Time span in the form of \"00:00:00\" or number of seconds to wait for the run to complete", defaultTimeout)
    {
    }
}

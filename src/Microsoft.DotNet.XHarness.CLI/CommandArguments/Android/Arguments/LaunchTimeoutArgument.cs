// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// Time to wait for boot completion.
/// </summary>
internal class LaunchTimeoutArgument : TimeSpanArgument
{
    public LaunchTimeoutArgument(TimeSpan defaultValue)
        : base("launch-timeout=|lt=", $"Time span in the form of \"00:00:00\" or number of seconds to wait for the device to boot to complete. Default is {defaultValue}", defaultValue)
    {
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Enables extra signaling between the TestRunner application and XHarness to work around problems in newer iOS.
/// </summary>
internal class SignalAppEndArgument : SwitchArgument
{
    public SignalAppEndArgument() : base("signal-app-end", "Tells the test application to signal back when tests have finished (some iOS/tvOS cannot detect this reliably otherwise)", false)
    {
    }
}

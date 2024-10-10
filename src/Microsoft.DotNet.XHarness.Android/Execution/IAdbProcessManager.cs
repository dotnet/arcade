// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.XHarness.Android.Execution;

// At some point all the process management APIs should be unified. For now I just added an 's' to ProcessExecutionResult to prevent accidental collision
public class ProcessExecutionResults
{
    public bool TimedOut { get; set; }
    public int ExitCode { get; set; }
    public bool Succeeded => !TimedOut && ExitCode == 0;
    public string StandardOutput { get; set; } = "";
    public string StandardError { get; set; } = "";

    public void ThrowIfFailed(string failureMessage)
    {
        if (!Succeeded)
        {
            throw new AdbFailureException(failureMessage + Environment.NewLine + this);
        }
    }

    public override string ToString()
    {
        var output = new StringBuilder();
        output.AppendLine($"Exit code: {ExitCode}");
        output.AppendLine($"Std out:{Environment.NewLine}{StandardOutput}{Environment.NewLine}");
        if (!string.IsNullOrEmpty(StandardError))
        {
            output.AppendLine($"Std err:{Environment.NewLine}{StandardError}{Environment.NewLine}");
        }

        return output.ToString();
    }
}

/// <summary>
/// Interface for calling the adb binary in a separate process.
/// </summary>
public interface IAdbProcessManager
{
    public string DeviceSerial { get; set; }

    public ProcessExecutionResults Run(string filename, IEnumerable<string> arguments, TimeSpan timeout);
}

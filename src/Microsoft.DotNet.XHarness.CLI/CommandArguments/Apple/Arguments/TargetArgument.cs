// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Test target (device, simulator, OS version...).
/// </summary>
internal class TargetArgument : Argument<TestTargetOs>
{
    public TargetArgument() : base("target=|targets=|t=", "Test target (device/simulator and OS)", TestTargetOs.None)
    {
    }

    public override void Action(string argumentValue)
    {
        try
        {
            Value = argumentValue.ParseAsAppRunnerTargetOs();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                $"Failed to parse test target '{argumentValue}'. Available targets are:" +
                GetAllowedValues(t => t.AsString(), invalidValues: TestTarget.None) +
                Environment.NewLine + Environment.NewLine +
                "You can also specify desired OS version, e.g. ios-simulator-64_13.4");
        }
    }

    public override void Validate()
    {
        if (Value == TestTargetOs.None)
        {
            throw new ArgumentException("No test target specified");
        }
    }
}

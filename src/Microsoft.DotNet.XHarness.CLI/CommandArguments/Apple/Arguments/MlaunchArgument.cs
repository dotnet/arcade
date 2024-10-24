// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Path to the mlaunch binary.
/// Default comes from the NuGet.
/// </summary>
internal class MlaunchArgument : Argument<string>
{
    public MlaunchArgument() : base("mlaunch=", "Path to the mlaunch binary. Defaults to mlaunch bundled with the XHarness nupkg", MacOSProcessManager.DetectMlaunchPath())
    {
    }

    public override void Action(string argumentValue) => Value = RootPath(argumentValue);

    public override void Validate()
    {
        if (!File.Exists(Value))
        {
            throw new ArgumentException(
                $"Failed to find mlaunch at {Value}. " +
                $"Make sure you specify --mlaunch or set the {EnvironmentVariables.Names.MLAUNCH_PATH} env var. " +
                $"See README.md for more information");
        }
    }
}

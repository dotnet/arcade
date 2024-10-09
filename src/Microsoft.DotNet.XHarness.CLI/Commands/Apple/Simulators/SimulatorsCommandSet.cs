// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple.Simulators;

/// <summary>
/// These commands allow management of Xcode iOS/WatchOS/tvOS Simulators on MacOS.
/// Originally taken from: https://github.com/xamarin/xamarin-macios/blob/master/tools/siminstaller
/// </summary>
public class SimulatorsCommandSet : CommandSet
{
    public SimulatorsCommandSet() : base("simulators")
    {
        Add(new ListCommand());
        Add(new FindCommand());
        Add(new InstallCommand());
        Add(new AppleResetSimulatorCommand(AppleCommandSet.GetAppleDependencies()));
    }
}

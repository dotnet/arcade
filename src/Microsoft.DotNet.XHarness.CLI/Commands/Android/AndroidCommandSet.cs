// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Commands.Android;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Android;

// Main Android command set that contains the plaform specific commands. 
// This allows the command line to support different options in different platforms.
// Regardless of whether underlying behavior matches, the goal is to have the same 
// arguments for both platforms and have unused functionality no-op in cases where it's not needed
public class AndroidCommandSet : CommandSet
{
    public AndroidCommandSet() : base("android")
    {
        // Common verbs shared with Android
        Add(new AndroidTestCommand());
        Add(new AndroidDeviceCommand());
        Add(new AndroidInstallCommand());
        Add(new AndroidRunCommand());
        Add(new AndroidUninstallCommand());
        Add(new AndroidAdbCommand());
        Add(new AndroidStateCommand());
    }
}

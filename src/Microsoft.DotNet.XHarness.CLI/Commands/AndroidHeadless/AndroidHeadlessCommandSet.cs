// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.CLI.Commands.Android;
using Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.AndroidHeadless;

// Main Android command set that contains the plaform specific commands. 
// This allows the command line to support different options in different platforms.
// Regardless of whether underlying behavior matches, the goal is to have the same 
// arguments for both platforms and have unused functionality no-op in cases where it's not needed
public class AndroidHeadlessCommandSet : CommandSet
{
    public AndroidHeadlessCommandSet() : base("android-headless")
    {
        // Common verbs shared with Android
        Add(new AndroidHeadlessTestCommand());
        Add(new AndroidHeadlessInstallCommand());
        Add(new AndroidHeadlessRunCommand());
        Add(new AndroidHeadlessUninstallCommand());
    }
}

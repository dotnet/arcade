// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal interface IAppleAppRunArguments : IAppleArguments
{
    TargetArgument Target { get; }
    OutputDirectoryArgument OutputDirectory { get; }
    TimeoutArgument Timeout { get; }
    DeviceNameArgument DeviceName { get; }
}

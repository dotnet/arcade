// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal interface IAndroidAppRunArguments
{
    PackageNameArgument PackageName { get; }
    OutputDirectoryArgument OutputDirectory { get; }
    TimeoutArgument Timeout { get; }
    LaunchTimeoutArgument LaunchTimeout { get; }
    DeviceIdArgument DeviceId { get; }
    ApiVersionArgument ApiVersion { get; }
}

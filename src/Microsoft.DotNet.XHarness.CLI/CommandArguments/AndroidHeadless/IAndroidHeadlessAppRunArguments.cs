// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal interface IAndroidHeadlessAppRunArguments
{
    TestPathArgument TestPath { get; }
    RuntimePathArgument RuntimePath { get; }
    TestAssemblyArgument TestAssembly { get; }
    TestScriptArgument TestScript { get; }
    OutputDirectoryArgument OutputDirectory { get; }
    TimeoutArgument Timeout { get; }
    LaunchTimeoutArgument LaunchTimeout { get; }
    DeviceIdArgument DeviceId { get; }
    ApiVersionArgument ApiVersion { get; }
}

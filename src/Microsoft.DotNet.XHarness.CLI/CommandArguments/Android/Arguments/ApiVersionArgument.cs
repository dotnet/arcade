// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class ApiVersionArgument : OptionalIntArgument
{
    public ApiVersionArgument()
        : base("api-version=|api=", "Target a device/emulator with given Android API version (level)")
    {
    }
}

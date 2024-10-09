// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Enable the lldb debugger to be used with the launched application.
/// </summary>
internal class EnableLldbArgument : SwitchArgument
{
    public EnableLldbArgument() : base("enable-lldb", "Allow to debug the launched application using lldb", false)
    {
    }
}

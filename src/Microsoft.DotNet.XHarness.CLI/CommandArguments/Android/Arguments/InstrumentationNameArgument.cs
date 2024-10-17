// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// If specified, attempt to run instrumentation with this name instead of the default for the supplied APK.
/// If a given package has multiple instrumentations, failing to specify this may cause execution failure.
/// </summary>
internal class InstrumentationNameArgument : StringArgument
{
    public InstrumentationNameArgument()
        : base("instrumentation=|i=", "If specified, attempt to run instrumentation with this name instead of the default for the supplied APK")
    {
    }
}

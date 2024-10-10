// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class WasmEngineArgument : Argument<WasmEngine?>
{
    public WasmEngineArgument()
        : base("engine=|e=", "Specifies the Wasm engine to be used", null)
    {
    }

    public override void Action(string argumentValue) =>
        Value = ParseArgument<WasmEngine>("engine", argumentValue);

    // Set WasmTime as default engine
    public override void Validate() => Value ??= WasmEngine.WasmTime;
}

/// <summary>
/// Specifies a name of a wasm engine used to run WASI application.
/// </summary>
internal enum WasmEngine
{
    /// <summary>
    /// WasmTime
    /// </summary>
    WasmTime,
}

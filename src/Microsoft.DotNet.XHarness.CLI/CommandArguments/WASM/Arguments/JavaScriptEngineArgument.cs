// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class JavaScriptEngineArgument : Argument<JavaScriptEngine?>
{
    public JavaScriptEngineArgument()
        : base("engine=|e=", "Specifies the JavaScript engine to be used", null)
    {
    }

    public override void Action(string argumentValue) =>
        Value = ParseArgument<JavaScriptEngine>("engine", argumentValue);

    public override void Validate()
    {
        if (Value == null)
        {
            throw new ArgumentException("Engine not specified");
        }
    }
}

/// <summary>
/// Specifies a name of a JavaScript engine used to run WASM application.
/// </summary>
internal enum JavaScriptEngine
{
    /// <summary>
    /// V8
    /// </summary>
    V8,
    /// <summary>
    /// JavaScriptCore
    /// </summary>
    JavaScriptCore,
    /// <summary>
    /// SpiderMonkey
    /// </summary>
    SpiderMonkey,
    /// <summary>
    /// NodeJS
    /// </summary>
    NodeJS,
}

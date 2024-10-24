// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;

internal class WasiTestCommandArguments : XHarnessCommandArguments, IWebServerArguments
{
    public WasmEngineArgument Engine { get; } = new();
    public WasmEngineLocationArgument EnginePath { get; } = new();
    public WasmEngineArguments EngineArgs { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

    public WebServerMiddlewareArgument WebServerMiddlewarePathsAndTypes { get; } = new();
    public WebServerHttpEnvironmentVariables WebServerHttpEnvironmentVariables { get; } = new();
    public WebServerHttpsEnvironmentVariables WebServerHttpsEnvironmentVariables { get; } = new();
    public WebServerUseHttpsArguments WebServerUseHttps { get; } = new();
    public WebServerUseCorsArguments WebServerUseCors { get; } = new();
    public WebServerUseCrossOriginPolicyArguments WebServerUseCrossOriginPolicy { get; } = new();
    public WebServerUseDefaultFilesArguments WebServerUseDefaultFiles { get; } = new();
    public bool IsWebServerEnabled => WebServerMiddlewarePathsAndTypes.Value.Count > 0;

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            Engine,
            EnginePath,
            EngineArgs,
            OutputDirectory,
            Timeout,
            ExpectedExitCode,

            WebServerMiddlewarePathsAndTypes,
            WebServerHttpEnvironmentVariables,
            WebServerHttpsEnvironmentVariables,
            WebServerUseHttps,
            WebServerUseCors,
            WebServerUseCrossOriginPolicy,
            WebServerUseDefaultFiles,
    };
}

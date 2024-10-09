// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class WebServerCommandArguments : XHarnessCommandArguments, IWebServerArguments
{
    public AppPathArgument AppPackagePath { get; } = new();

    public WebServerMiddlewareArgument WebServerMiddlewarePathsAndTypes { get; } = new();
    public WebServerHttpEnvironmentVariables WebServerHttpEnvironmentVariables { get; } = new();
    public WebServerHttpsEnvironmentVariables WebServerHttpsEnvironmentVariables { get; } = new();
    public WebServerUseHttpsArguments WebServerUseHttps { get; } = new();
    public WebServerUseCorsArguments WebServerUseCors { get; } = new();
    public WebServerUseCrossOriginPolicyArguments WebServerUseCrossOriginPolicy { get; } = new();
    public WebServerUseDefaultFilesArguments WebServerUseDefaultFiles { get; } = new();
    public bool IsWebServerEnabled => WebServerMiddlewarePathsAndTypes.Value.Count > 0;

    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppPackagePath,
        Timeout,
        WebServerMiddlewarePathsAndTypes,
        WebServerHttpEnvironmentVariables,
        WebServerHttpsEnvironmentVariables,
        WebServerUseHttps,
        WebServerUseCors,
        WebServerUseCrossOriginPolicy,
        WebServerUseDefaultFiles,
    };
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal interface IWebServerArguments
{
    bool IsWebServerEnabled { get; }
    WebServerMiddlewareArgument WebServerMiddlewarePathsAndTypes { get; }
    WebServerHttpEnvironmentVariables WebServerHttpEnvironmentVariables { get; }
    WebServerHttpsEnvironmentVariables WebServerHttpsEnvironmentVariables { get; }
    WebServerUseHttpsArguments WebServerUseHttps { get; }
    WebServerUseCorsArguments WebServerUseCors { get; }
    WebServerUseCrossOriginPolicyArguments WebServerUseCrossOriginPolicy { get; }
    WebServerUseDefaultFilesArguments WebServerUseDefaultFiles { get; }
}

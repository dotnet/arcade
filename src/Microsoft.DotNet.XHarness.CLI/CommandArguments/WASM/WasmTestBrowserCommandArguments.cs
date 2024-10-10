// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class WasmTestBrowserCommandArguments : XHarnessCommandArguments, IWebServerArguments
{
    public AppPathArgument AppPackagePath { get; } = new();
    public BrowserArgument Browser { get; } = new();
    public BrowserLocationArgument BrowserLocation { get; } = new();
    public BrowserArguments BrowserArgs { get; } = new();
    public HTMLFileArgument HTMLFile { get; } = new("index.html");
    public ErrorPatternsFileArgument ErrorPatternsFile { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)ExitCode.SUCCESS);
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public DebuggerPortArgument DebuggerPort { get; set; } = new();
    public NoIncognitoArgument NoIncognito { get; } = new();
    public NoHeadlessArgument NoHeadless { get; } = new();
    public NoQuitArgument NoQuit { get; } = new();
    public BackgroundThrottlingArgument BackgroundThrottling { get; } = new();
    public LocaleArgument Locale { get; } = new("en-US");
    public PageLoadStrategyArgument PageLoadStrategy { get; } = new(OpenQA.Selenium.PageLoadStrategy.Normal);

    public SymbolMapFileArgument SymbolMapFileArgument { get; } = new();
    public SymbolicatePatternsFileArgument SymbolicatePatternsFileArgument { get; } = new();
    public SymbolicatorArgument SymbolicatorArgument { get; } = new();
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
            AppPackagePath,
            Browser,
            BrowserLocation,
            BrowserArgs,
            HTMLFile,
            ErrorPatternsFile,
            ExpectedExitCode,
            OutputDirectory,
            Timeout,
            DebuggerPort,
            NoIncognito,
            NoHeadless,
            NoQuit,
            BackgroundThrottling,
            Locale,
            PageLoadStrategy,
            SymbolMapFileArgument,
            SymbolicatePatternsFileArgument,
            SymbolicatorArgument,
            WebServerMiddlewarePathsAndTypes,
            WebServerHttpEnvironmentVariables,
            WebServerHttpsEnvironmentVariables,
            WebServerUseHttps,
            WebServerUseCors,
            WebServerUseCrossOriginPolicy,
            WebServerUseDefaultFiles,
    };

    public override void Validate()
    {
        base.Validate();

        if (!string.IsNullOrEmpty(BrowserLocation))
        {
            if (Browser == Wasm.Browser.Safari)
            {
                throw new ArgumentException("Safari driver doesn't support custom browser path");
            }

            if (!File.Exists(BrowserLocation))
            {
                throw new ArgumentException($"Could not find browser at {BrowserLocation}");
            }
        }

        if (DebuggerPort.Value != null || NoQuit)
        {
            NoHeadless.Set(true);
        }
    }
}

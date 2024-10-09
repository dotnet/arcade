// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WebServerCommand : XHarnessCommand<WebServerCommandArguments>
{
    private const string CommandHelp = "Starts a webserver";

    protected override string CommandUsage { get; } = "wasm webserver [OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    protected override WebServerCommandArguments Arguments { get; } = new();

    public WebServerCommand()
        : base(TargetPlatform.WASM, "webserver", allowsExtraArgs: true, new ServiceCollection(), CommandHelp)
    {
    }

    protected override async Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var cts = new CancellationTokenSource();
        var webServerOptions = WebServer.TestWebServerOptions.FromArguments(Arguments);
        webServerOptions.ContentRoot = Arguments.AppPackagePath;
        ServerURLs serverURLs = await WebServer.Start(
            webServerOptions,
            logger,
            cts.Token);

        logger.LogInformation($"Now listening on: http://{serverURLs.Http}");
        if (!string.IsNullOrEmpty(serverURLs.Https))
            logger.LogInformation($"Now listening on: https://{serverURLs.Https}");

        await Task.Delay(Arguments.Timeout, cts.Token);
        if (cts.Token.IsCancellationRequested)
        {
            logger.LogError("Token cancelled for unknown reasons, exiting.");
            return ExitCode.GENERAL_FAILURE;
        }
        else
        {
            logger.LogInformation($"Stopping the webserver after the timeout of {Arguments.Timeout}");
            return ExitCode.SUCCESS;
        }
    }
}

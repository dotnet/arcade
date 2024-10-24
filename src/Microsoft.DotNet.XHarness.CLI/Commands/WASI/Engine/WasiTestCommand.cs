// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasi;
using Microsoft.DotNet.XHarness.CLI.Commands.Wasm;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasi;
internal class WasiTestCommand : XHarnessCommand<WasiTestCommandArguments>
{
    private const string CommandHelp = "Executes tests on WASI using a selected engine";

    protected override WasiTestCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "wasi test [OPTIONS] -- [ENGINE OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    public WasiTestCommand() : base(TargetPlatform.WASI, "test", true, new ServiceCollection(), CommandHelp)
    {
    }

    protected override async Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var processManager = ProcessManagerFactory.CreateProcessManager();

        string engineBinary = Arguments.Engine.Value switch
        {
            WasmEngine.WasmTime => "wasmtime",
            _ => throw new ArgumentException("Engine not set")
        };

        if (!string.IsNullOrEmpty(Arguments.EnginePath.Value))
        {
            engineBinary = Arguments.EnginePath.Value;
            if (Path.IsPathRooted(engineBinary) && !File.Exists(engineBinary))
                throw new ArgumentException($"Could not find wasi engine at the specified path - {engineBinary}");
        }
        else
        {
            if (!FileUtils.TryFindExecutableInPATH(engineBinary, out string? foundBinary, out string? errorMessage))
            {
                logger.LogCritical($"The engine binary `{engineBinary}` was not found. {errorMessage}");
                return ExitCode.APP_LAUNCH_FAILURE;
            }
            engineBinary = foundBinary;
        }

        var cts = new CancellationTokenSource();
        try
        {
            logger.LogInformation($"Using wasm engine {Arguments.Engine.Value} from path {engineBinary}");
            await PrintVersionAsync(Arguments.Engine.Value.Value, engineBinary);

            ServerURLs? serverURLs = null;
            if (Arguments.IsWebServerEnabled)
            {
                serverURLs = await WebServer.Start(
                    Arguments,
                    logger,
                    cts.Token);
            }

            var engineArgs = new List<string>();
            engineArgs.AddRange(Arguments.EngineArgs.Value);

            if (Arguments.IsWebServerEnabled)
            {
                foreach (var envVariable in Arguments.WebServerHttpEnvironmentVariables.Value)
                {
                    engineArgs.Add($"--env");
                    engineArgs.Add($"{envVariable}={serverURLs!.Http}");
                }
                if (Arguments.WebServerUseHttps)
                {
                    foreach (var envVariable in Arguments.WebServerHttpsEnvironmentVariables.Value)
                    {
                        engineArgs.Add($"--env");
                        engineArgs.Add($"{envVariable}={serverURLs!.Https}");
                    }
                }
            }
            
            engineArgs.AddRange(PassThroughArguments);

            var xmlResultsFilePath = Path.Combine(Arguments.OutputDirectory, "testResults.xml");
            File.Delete(xmlResultsFilePath);

            var stdoutFilePath = Path.Combine(Arguments.OutputDirectory, "wasi-console.log");
            File.Delete(stdoutFilePath);

            var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath,
                                                             stdoutFilePath,
                                                             logger);
            var logProcessorTask = Task.Run(() => logProcessor.RunAsync(cts.Token));

            var processTask = processManager.ExecuteCommandAsync(
                engineBinary,
                engineArgs,
                log: new CallbackLog(m => logger.LogInformation(m.Trim())),
                stdoutLog: new CallbackLog(msg => logProcessor.Invoke(msg)),
                stderrLog: new CallbackLog(logProcessor.ProcessErrorMessage),
                Arguments.Timeout);

            var tasks = new Task[]
            {
                logProcessorTask,
                processTask,
                Task.Delay(Arguments.Timeout)
            };

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);
            if (task == tasks[^1] || cts.IsCancellationRequested || task.IsCanceled)
            {
                logger.LogError($"Tests timed out after {((TimeSpan)Arguments.Timeout).TotalSeconds}secs");
                if (!cts.IsCancellationRequested)
                    cts.Cancel();

                return ExitCode.TIMED_OUT;
            }

            if (task.IsFaulted)
            {
                logger.LogError($"task faulted {task.Exception}");
                throw task.Exception!;
            }

            // if the log processor completed without errors, then the
            // process should be done too, or about to be done!
            var result = await processTask;
            ExitCode logProcessorExitCode = await logProcessor.CompleteAndFlushAsync();
            if (logProcessor.ForwardedExitCode != null)
            {
                // until WASI can work with unix exit code https://github.com/WebAssembly/wasi-cli/pull/44
                result.ExitCode = logProcessor.ForwardedExitCode.Value;
            }

            if (result.ExitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {result.ExitCode} but {Arguments.ExpectedExitCode} was expected");
                return ExitCode.GENERAL_FAILURE;
            }
            else
            {
                logger.LogInformation("Application has finished with exit code: " + result.ExitCode);
                // return SUCCESS if logProcess also returned SUCCESS
                return logProcessorExitCode;
            }
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 2)
        {
            logger.LogCritical($"The engine binary `{engineBinary}` was not found");
            return ExitCode.APP_LAUNCH_FAILURE;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }

        Task PrintVersionAsync(WasmEngine engine, string engineBinary)
        {
            return processManager.ExecuteCommandAsync(
                        engineBinary,
                        new[] { "--version" },
                        log: new CallbackLog(m => logger.LogDebug(m.Trim())),
                        stdoutLog: new CallbackLog(msg => logger.LogInformation(msg.Trim())),
                        stderrLog: new CallbackLog(msg => logger.LogError(msg.Trim())),
                        TimeSpan.FromSeconds(10));
        }
    }
}

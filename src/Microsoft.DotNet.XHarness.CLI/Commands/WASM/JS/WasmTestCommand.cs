// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WasmTestCommand : XHarnessCommand<WasmTestCommandArguments>
{
    private const string CommandHelp = "Executes tests on WASM using a selected JavaScript engine";

    protected override WasmTestCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "wasm test [OPTIONS] -- [ENGINE OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    public WasmTestCommand() : base(TargetPlatform.WASM, "test", true, new ServiceCollection(), CommandHelp)
    {
    }

    protected override async Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var processManager = ProcessManagerFactory.CreateProcessManager();

        string engineBinary = Arguments.Engine.Value switch
        {
            JavaScriptEngine.V8 => "v8",
            JavaScriptEngine.JavaScriptCore => "jsc",
            JavaScriptEngine.SpiderMonkey => "sm",
            JavaScriptEngine.NodeJS => "node",
            _ => throw new ArgumentException("Engine not set")
        };

        if (!string.IsNullOrEmpty(Arguments.EnginePath.Value))
        {
            engineBinary = Arguments.EnginePath.Value;
            if (Path.IsPathRooted(engineBinary) && !File.Exists(engineBinary))
                throw new ArgumentException($"Could not find js engine at the specified path - {engineBinary}");
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

        var serviceProvider = Services.BuildServiceProvider();
        var diagnosticsData = serviceProvider.GetRequiredService<IDiagnosticsData>();
        diagnosticsData.Target = engineBinary;

        var cts = new CancellationTokenSource();
        try
        {
            logger.LogInformation($"Using js engine {Arguments.Engine.Value} from path {engineBinary}");
            string? versionString = await GetVersionAsync(Arguments.Engine.Value.Value, engineBinary);
            diagnosticsData.TargetOS = versionString;

            ServerURLs? serverURLs = null;
            if (Arguments.IsWebServerEnabled)
            {
                serverURLs = await WebServer.Start(
                    Arguments,
                    logger,
                    cts.Token);
            }

            var engineArgs = new List<string>();

            if (Arguments.Engine == JavaScriptEngine.V8)
            {
                // v8 needs this flag to enable WASM support
                engineArgs.Add("--expose_wasm");
            }

            engineArgs.AddRange(Arguments.EngineArgs.Value);
            engineArgs.Add(Arguments.JSFile);

            if (Arguments.Engine == JavaScriptEngine.V8 || Arguments.Engine == JavaScriptEngine.JavaScriptCore)
            {
                // v8/jsc want arguments to the script separated by "--", others don't
                engineArgs.Add("--");
            }

            if (Arguments.IsWebServerEnabled)
            {
                foreach (var envVariable in Arguments.WebServerHttpEnvironmentVariables.Value)
                {
                    engineArgs.Add($"--setenv={envVariable}={serverURLs!.Http}");
                }
                if (Arguments.WebServerUseHttps)
                {
                    foreach (var envVariable in Arguments.WebServerHttpsEnvironmentVariables.Value)
                    {
                        engineArgs.Add($"--setenv={envVariable}={serverURLs!.Https}");
                    }
                }
            }

            engineArgs.AddRange(PassThroughArguments);

            var xmlResultsFilePath = Path.Combine(Arguments.OutputDirectory, "testResults.xml");
            File.Delete(xmlResultsFilePath);

            var stdoutFilePath = Path.Combine(Arguments.OutputDirectory, "wasm-console.log");
            File.Delete(stdoutFilePath);

            var symbolicator = WasmSymbolicatorBase.Create(Arguments.SymbolicatorArgument.GetLoadedTypes().FirstOrDefault(),
                                                           Arguments.SymbolMapFileArgument,
                                                           Arguments.SymbolicatePatternsFileArgument,
                                                           logger);

            var logProcessor = new WasmTestMessagesProcessor(xmlResultsFilePath,
                                                             stdoutFilePath,
                                                             logger,
                                                             Arguments.ErrorPatternsFile,
                                                             symbolicator);
            var logProcessorTask = Task.Run(() => logProcessor.RunAsync(cts.Token));

            var processTask = processManager.ExecuteCommandAsync(
                engineBinary,
                engineArgs,
                log: new CallbackLog(m => logger.LogInformation(m)),
                stdoutLog: new CallbackLog(msg => logProcessor.Invoke(msg)),
                stderrLog: new CallbackLog(logProcessor.ProcessErrorMessage),
                Arguments.Timeout,
                // Node respects LANG only, ignores LANGUAGE
                environmentVariables: Arguments.Engine.Value == JavaScriptEngine.NodeJS ?
                                    new Dictionary<string, string>() { {"LANG", Arguments.Locale} } :
                                    null);

            TaskCompletionSource wasmExitReceivedTcs = logProcessor.WasmExitReceivedTcs;
            var tasks = new Task[]
            {
                wasmExitReceivedTcs.Task,
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

            // give messages bit more time
            if (!wasmExitReceivedTcs.Task.IsCompleted)
            {
                await Task.WhenAny(new Task[] { wasmExitReceivedTcs.Task, Task.Delay(200) }).ConfigureAwait(false);
            }

            if (result.ExitCode != Arguments.ExpectedExitCode)
            {
                logger.LogError($"Application has finished with exit code {result.ExitCode} but {Arguments.ExpectedExitCode} was expected");
                return ExitCode.GENERAL_FAILURE;
            }
            else
            {
                if(!wasmExitReceivedTcs.Task.IsCompletedSuccessfully)
                {
                    logger.LogError("Application exited without sending a wasm exit message.");
                    // TODO return RETURN_CODE_NOT_SET when we are more confident that it doesn't happen very often
                    // return ExitCode.RETURN_CODE_NOT_SET;
                }
                if (logProcessor.LineThatMatchedErrorPattern != null)
                {
                    logger.LogError("Application exited with the expected exit code: {result.ExitCode}."
                                    + $" But found a line matching an error pattern: {logProcessor.LineThatMatchedErrorPattern}");
                    return ExitCode.APP_CRASH;
                }

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

        async Task<string?> GetVersionAsync(JavaScriptEngine engine, string engineBinary)
        {
            string[] args;
            switch (engine)
            {
                case JavaScriptEngine.V8:
                    args = new[] { "-e", "console.log(this.version())" };
                    break;
                case JavaScriptEngine.NodeJS:
                    args = new[] { "--version" };
                    break;
                default:
                    return null;
            };

            StringBuilder output = new();
            await processManager.ExecuteCommandAsync(
                        engineBinary,
                        args,
                        log: new CallbackLog(m => logger.LogDebug(m.Trim())),
                        stdoutLog: new CallbackLog(msg =>
                        {
                            logger.LogInformation(msg.Trim());
                            output.Append(msg.Trim());
                        }),
                        stderrLog: new CallbackLog(msg => logger.LogError(msg.Trim())),
                        TimeSpan.FromSeconds(10));

            return output.ToString();
        }
    }

}

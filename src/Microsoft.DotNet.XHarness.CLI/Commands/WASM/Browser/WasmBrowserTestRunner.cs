// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

using SeleniumLogLevel = OpenQA.Selenium.LogLevel;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasm;

internal class WasmBrowserTestRunner
{
    private readonly WasmTestBrowserCommandArguments _arguments;
    private readonly ILogger _logger;
    private readonly IEnumerable<string> _passThroughArguments;
    private readonly WasmTestMessagesProcessor _messagesProcessor;

    // Messages from selenium prepend the url, and location where the message originated
    // Eg. `foo` becomes `http://localhost:8000/xyz.js 0:12 "foo"
    static readonly Regex s_consoleLogRegex = new(@"^\s*[a-z]*://[^\s]+\s+\d+:\d+\s+""(.*)""\s*$", RegexOptions.Compiled);

    public WasmBrowserTestRunner(WasmTestBrowserCommandArguments arguments, IEnumerable<string> passThroughArguments,
                                        WasmTestMessagesProcessor messagesProcessor, ILogger logger)
    {
        _arguments = arguments;
        _logger = logger;
        _passThroughArguments = passThroughArguments;
        _messagesProcessor = messagesProcessor;
    }

    public async Task<ExitCode> RunTestsWithWebDriver(DriverService driverService, IWebDriver driver)
    {
        var htmlFilePath = Path.Combine(_arguments.AppPackagePath, _arguments.HTMLFile.Value);
        if (!File.Exists(htmlFilePath))
        {
            _logger.LogError($"Could not find html file {htmlFilePath}");
            return ExitCode.GENERAL_FAILURE;
        }

        var cts = new CancellationTokenSource();
        try
        {
            var consolePumpTcs = new TaskCompletionSource<bool>();
            var logProcessorTask = Task.Run(() => _messagesProcessor.RunAsync(cts.Token));

            var webServerOptions = WebServer.TestWebServerOptions.FromArguments(_arguments);
            webServerOptions.ContentRoot = _arguments.AppPackagePath;
            webServerOptions.OnConsoleConnected = socket => RunConsoleMessagesPump(socket, cts.Token);
            ServerURLs serverURLs = await WebServer.Start(
                webServerOptions,
                _logger,
                cts.Token);

            string testUrl = BuildUrl(serverURLs);

            var seleniumLogMessageTask = Task.Run(() => RunSeleniumLogMessagePump(driver, cts.Token), cts.Token);
            cts.CancelAfter(_arguments.Timeout);

            _logger.LogDebug($"Opening in browser: {testUrl}");
            driver.Navigate().GoToUrl(testUrl);

            TaskCompletionSource wasmExitReceivedTcs = _messagesProcessor.WasmExitReceivedTcs;
            var tasks = new Task[]
            {
                    wasmExitReceivedTcs.Task,
                    consolePumpTcs.Task,
                    seleniumLogMessageTask,
                    logProcessorTask,
                    Task.Delay(_arguments.Timeout)
            };

            if (_arguments.BackgroundThrottling)
            {
                // throttling only happens when the page is not visible
                driver.Manage().Window.Minimize();
            }

            var task = await Task.WhenAny(tasks).ConfigureAwait(false);

            ExitCode logProcessorExitCode = ExitCode.SUCCESS;
            if (task != logProcessorTask && !task.IsFaulted)
                logProcessorExitCode = await _messagesProcessor.CompleteAndFlushAsync();

            if (task == tasks[^1] || cts.IsCancellationRequested)
            {
                if (driverService.IsRunning)
                {
                    // Selenium isn't able to kill chrome in this case :/
                    int pid = driverService.ProcessId;
                    var p = Process.GetProcessById(pid);
                    if (p != null)
                    {
                        _logger.LogError($"Tests timed out. Killing driver service pid {pid}");
                        p.Kill(true);
                    }
                }

                // timed out
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
                return ExitCode.TIMED_OUT;
            }

            if (task == wasmExitReceivedTcs.Task && wasmExitReceivedTcs.Task.IsCompletedSuccessfully)
            {
                _logger.LogTrace($"Looking for `tests_done` element, to get the exit code");
                var testsDoneElement = new WebDriverWait(driver, TimeSpan.FromSeconds(30))
                                            .Until(e => e.FindElement(By.Id("tests_done")));

                if (int.TryParse(testsDoneElement.Text, out var code))
                {
                    var appExitCode = (ExitCode)Enum.ToObject(typeof(ExitCode), code);
                    if (logProcessorExitCode != ExitCode.SUCCESS)
                    {
                        _logger.LogInformation($"Application has finished with exit code {appExitCode}. But the log processor failed with {logProcessorExitCode}.");
                        return logProcessorExitCode;
                    }

                    return appExitCode;
                }

                return ExitCode.RETURN_CODE_NOT_SET;
            }

            if (task.IsFaulted)
            {
                _logger.LogDebug($"task faulted {task.Exception}");
                throw task.Exception!;
            }

            return ExitCode.TIMED_OUT;
        }
        finally
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }
    }

    private async Task RunConsoleMessagesPump(WebSocket socket, CancellationToken token)
    {
        byte[] buff = new byte[4000];
        var mem = new MemoryStream();
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (socket.State != WebSocketState.Open)
                {
                    _logger.LogError($"DevToolsProxy: Socket is no longer open.");
                    return;
                }

                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buff), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return;

                mem.Write(buff, 0, result.Count);

                if (result.EndOfMessage)
                {
                    var line = Encoding.UTF8.GetString(mem.GetBuffer(), 0, (int)mem.Length);
                    line += Environment.NewLine;

                    await _messagesProcessor.InvokeAsync(line, token);
                    mem.SetLength(0);
                    mem.Seek(0, SeekOrigin.Begin);
                }
            }
        }
        catch (WebSocketException wse)
        {
            // this could happen when WebWorker is closed or when browser died
            _logger.LogDebug($"RunConsoleMessagesPump failed: {wse}");
        }
        catch (OperationCanceledException oce)
        {
            if (!token.IsCancellationRequested)
                _logger.LogDebug($"RunConsoleMessagesPump cancelled: {oce}");
        }
        finally
        {
            _logger.LogDebug($"Reading console messages from websocket stopped");
        }
    }

    // This listens for any `console.log` messages.
    // Since we pipe messages from managed code, and console.* to the websocket,
    // this wouldn't normally get much. But listening on this to catch any messages
    // that we miss piping to the websocket.
    private void RunSeleniumLogMessagePump(IWebDriver driver, CancellationToken token)
    {
        try
        {
            ILogs logs = driver.Manage().Logs;
            while (!token.IsCancellationRequested)
            {
                foreach (var logType in logs.AvailableLogTypes)
                {
                    foreach (var logEntry in logs.GetLog(logType))
                    {
                        if (logEntry.Level == SeleniumLogLevel.Severe)
                        {
                            // These are errors from the browser, some of which might be
                            // thrown as part of tests. So, we can't differentiate when
                            // it is an error that we can ignore, vs one that should stop
                            // the execution completely.
                            //
                            // Note: these could be received out-of-order as compared to
                            // console messages via the websocket.
                            //
                            // (see commit message for more info)
                            _logger.LogError($"[out of order message from the {logType}]: {logEntry.Message}");
                            continue;
                        }

                        var match = s_consoleLogRegex.Match(Regex.Unescape(logEntry.Message));
                        string msg = match.Success ? match.Groups[1].Value : logEntry.Message;
                        _messagesProcessor.Invoke(msg);
                    }
                }
            }
        }
        catch (WebDriverException wde) when (wde.Message.Contains("timed out after"))
        { }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed trying to read log messages via selenium: {ex}");
            throw;
        }
    }

    private string BuildUrl(ServerURLs serverURLs)
    {
        var uriBuilder = new UriBuilder($"{serverURLs.Http}/{_arguments.HTMLFile}");
        var sb = new StringBuilder();

        if (_arguments.DebuggerPort.Value != null)
            sb.Append($"arg=--debug");

        foreach (var envVariable in _arguments.WebServerHttpEnvironmentVariables.Value)
        {
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append($"arg={HttpUtility.UrlEncode($"--setenv={envVariable}={serverURLs!.Http}")}");
        }

        if (_arguments.WebServerUseHttps)
        {
            foreach (var envVariable in _arguments.WebServerHttpsEnvironmentVariables.Value)
            {
                if (sb.Length > 0)
                    sb.Append('&');
                sb.Append($"arg={HttpUtility.UrlEncode($"--setenv={envVariable}={serverURLs!.Https}")}");
            }
        }

        foreach (var arg in _passThroughArguments)
        {
            if (sb.Length > 0)
                sb.Append('&');

            sb.Append($"arg={HttpUtility.UrlEncode(arg)}");
        }

        if (sb.Length > 0)
            sb.Append('&');

        sb.Append($"arg=-verbosity&arg={VerbosityToString()}");

        uriBuilder.Query = sb.ToString();
        return uriBuilder.ToString();
    }

    // MinimumLogLevel.Critical,
    // MinimumLogLevel.Error,
    // MinimumLogLevel.Warning,
    // MinimumLogLevel.Info,
    // MinimumLogLevel.Debug,
    // MinimumLogLevel.Verbose
    private string VerbosityToString() => _arguments.Verbosity.Value switch
    {
        LogLevel.Trace => "Verbose",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Info",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        LogLevel.None => "Critical",
        _ => throw new NotSupportedException($"The value '{_arguments.Verbosity.Value}' is not supported in conversion to MinimumLogLevel")
    };
}

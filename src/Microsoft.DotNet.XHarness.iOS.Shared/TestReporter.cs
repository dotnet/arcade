// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using ExceptionLogger = System.Action<int, string>;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

/// <summary>
/// Class that gets the result of an executed test application, parses the results and provides information
/// about the success or failure of the execution.
/// </summary>
public class TestReporter : ITestReporter
{
    private const string TimeoutMessage = "Test run timed out after {0} minute(s).";
    private const string CompletionMessage = "Test run completed";
    private const string FailureMessage = "Test run failed";

    private readonly ISimpleListener _listener;
    private readonly IFileBackedLog _mainLog;
    private readonly ILogs _crashLogs;
    private readonly IReadableLog _runLog;
    private readonly ILogs _logs;
    private readonly ICrashSnapshotReporter _crashReporter;
    private readonly IResultParser _resultParser;
    private readonly AppBundleInformation _appInfo;
    private readonly RunMode _runMode;
    private readonly XmlResultJargon _xmlJargon;
    private readonly IMlaunchProcessManager _processManager;
    private readonly string? _deviceName;
    private readonly TimeSpan _timeout;
    private readonly Stopwatch _timeoutWatch;

    /// <summary>
    /// Additional logs that will be sent with the report in case of a failure.
    /// Used by the Xamarin.Xharness project to add BuildTask logs.
    /// </summary>
    private readonly string? _additionalLogsDirectory;

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Callback needed for the Xamarin.Xharness project that does extra logging in case of a crash.
    /// </summary>
    private readonly ExceptionLogger? _exceptionLogger;

    private bool _waitedForExit = true;
    private bool _launchFailure;
    private bool _isSimulatorTest;
    private bool _timedout;
    private readonly bool _generateHtml;

    public ILog CallbackLog { get; private set; }

    public bool? Success { get; private set; }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    public bool ResultsUseXml => _xmlJargon != XmlResultJargon.Missing;

    private bool TestExecutionStarted => _listener.ConnectedTask.IsCompleted && _listener.ConnectedTask.Result;

    public TestReporter(
        IMlaunchProcessManager processManager,
        IFileBackedLog mainLog,
        IReadableLog runLog,
        ILogs logs,
        ICrashSnapshotReporter crashReporter,
        ISimpleListener simpleListener,
        IResultParser parser,
        AppBundleInformation appInformation,
        RunMode runMode,
        XmlResultJargon xmlJargon,
        string? device,
        TimeSpan timeout,
        string? additionalLogsDirectory = null,
        ExceptionLogger? exceptionLogger = null,
        bool generateHtml = false)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _deviceName = device; // can be null on simulators
        _listener = simpleListener ?? throw new ArgumentNullException(nameof(simpleListener));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _runLog = runLog ?? throw new ArgumentNullException(nameof(runLog));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _crashReporter = crashReporter ?? throw new ArgumentNullException(nameof(crashReporter));
        _crashLogs = new Logs(logs.Directory);
        _resultParser = parser ?? throw new ArgumentNullException(nameof(parser));
        _appInfo = appInformation ?? throw new ArgumentNullException(nameof(appInformation));
        _runMode = runMode;
        _xmlJargon = xmlJargon;
        _timeout = timeout;
        _additionalLogsDirectory = additionalLogsDirectory;
        _exceptionLogger = exceptionLogger;
        _timeoutWatch = Stopwatch.StartNew();
        _generateHtml = generateHtml;

        CallbackLog = new CallbackLog(line =>
        {
            // MT1111: Application launched successfully, but it's not possible to wait for the app to exit as
            // requested because it's not possible to detect app termination when launching using gdbserver
            _waitedForExit &= line?.Contains("MT1111: ") != true;
            if (line?.Contains("error MT1007") == true)
            {
                _launchFailure = true;
            }
        });
    }

    /// <summary>
    /// Parse the run log and decide if we managed to start the process or not
    /// </summary>
    private async Task<int> GetPidFromRunLog()
    {
        int pid = -1;

        using var reader = _runLog.GetReader(); // diposed at the end of the method, which is what we want
        if (reader.Peek() == -1)
        {
            // Empty file! If the app never connected to our listener, it probably never launched
            if (!_listener.ConnectedTask.IsCompleted || !_listener.ConnectedTask.Result)
            {
                _launchFailure = true;
            }
        }
        else
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();

                if (line == null)
                {
                    continue;
                }

                if (line.StartsWith("Application launched. PID = ", StringComparison.Ordinal))
                {
                    var pidstr = line.Substring("Application launched. PID = ".Length);
                    if (!int.TryParse(pidstr, out pid))
                    {
                        _mainLog.WriteLine("Could not parse pid: {0}", pidstr);
                    }
                }
                else if (line.Contains("Xamarin.Hosting: Launched ") && line.Contains(" with pid "))
                {
                    var pidstr = line.Substring(line.LastIndexOf(' '));
                    if (!int.TryParse(pidstr, out pid))
                    {
                        _mainLog.WriteLine("Could not parse pid: {0}", pidstr);
                    }
                }
                else if (line.Contains("error MT1008"))
                {
                    _launchFailure = true;
                }
            }
        }

        return pid;
    }

    /// <summary>
    /// Parse the main log to get the pid
    /// </summary>
    private async Task<int> GetPidFromMainLog()
    {
        int pid = -1;
        using var log_reader = _mainLog.GetReader(); // dispose when we leave the method, which is what we want
        string? line;
        while ((line = await log_reader.ReadLineAsync()) != null)
        {
            const string str = "was launched with pid '";
            var idx = line.IndexOf(str, StringComparison.Ordinal);
            if (idx > 0)
            {
                idx += str.Length;
                var next_idx = line.IndexOf('\'', idx);
                if (next_idx > idx)
                {
                    int.TryParse(line.Substring(idx, next_idx - idx), out pid);
                }
            }
            if (pid != -1)
            {
                return pid;
            }
        }
        return pid;
    }

    /// <summary>
    /// Return the reason for a crash found in a log
    /// </summary>
    private void GetCrashReason(int pid, IReadableLog crashLog, out string? crashReason)
    {
        crashReason = null;
        using var crashReader = crashLog.GetReader(); // dispose when we leave the method
        var text = crashReader.ReadToEnd();

        var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(text), new XmlDictionaryReaderQuotas());
        var doc = new XmlDocument();
        doc.Load(reader);
        foreach (XmlNode? node in doc.SelectNodes($"/root/processes/item[pid = '" + pid + "']"))
        {
            Console.WriteLine(node?.InnerXml);
            Console.WriteLine(node?.SelectSingleNode("reason")?.InnerText);
            crashReason = node?.SelectSingleNode("reason")?.InnerText;
        }
    }

    /// <summary>
    /// Return if the tcp connection with the device failed
    /// </summary>
    private async Task<bool> TcpConnectionFailed()
    {
        using var reader = new StreamReader(_mainLog.FullPath);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.Contains("Couldn't establish a TCP connection with any of the hostnames"))
            {
                return true;
            }
        }
        return false;
    }

    private Task KillAppProcess(int pid, CancellationTokenSource cancellationSource)
    {
        var launchTimedout = cancellationSource.IsCancellationRequested;
        var timeoutType = launchTimedout ? "Launch" : "Completion";
        _mainLog.WriteLine($"{timeoutType} timed out after {_timeoutWatch.Elapsed.TotalSeconds} seconds");
        return _processManager.KillTreeAsync(pid, _mainLog, true);
    }

    private async Task CollectResult(ProcessExecutionResult runResult)
    {
        if (!_waitedForExit && !runResult.TimedOut)
        {
            // mlaunch couldn't wait for exit for some reason. Let's assume the app exits when the test listener completes.
            _mainLog.WriteLine("Waiting for listener to complete, since mlaunch won't tell.");
            if (!await _listener.CompletionTask.TimeoutAfter(_timeout - _timeoutWatch.Elapsed))
            {
                runResult.TimedOut = true;
            }
        }

        if (runResult.TimedOut)
        {
            _timedout = true;
            Success = false;
            _mainLog.WriteLine(TimeoutMessage, _timeout.TotalMinutes);
        }
        else if (runResult.Succeeded)
        {
            _mainLog.WriteLine(CompletionMessage);
            Success = true;
        }
        else
        {
            _mainLog.WriteLine(FailureMessage);
            Success = false;
        }
    }

    public void LaunchCallback(Task<bool> launchResult)
    {
        if (launchResult.IsFaulted)
        {
            _mainLog.WriteLine($"Test execution failed: {launchResult.Exception}");
            return;
        }

        if (launchResult.IsCanceled)
        {
            _mainLog.WriteLine("Test execution was cancelled");
            return;
        }

        if (launchResult.Result)
        {
            _mainLog.WriteLine("Test execution started");
            return;
        }

        _cancellationTokenSource.Cancel();
        _timedout = true;

        if (TestExecutionStarted)
        {
            _mainLog.WriteLine($"Test execution timed out after {_timeoutWatch.Elapsed.TotalMinutes:0.##} minutes");
            return;
        }

        _mainLog.WriteLine($"Test failed to start in {_timeoutWatch.Elapsed.TotalMinutes:0.##} minutes");
    }

    public async Task CollectSimulatorResult(ProcessExecutionResult runResult)
    {
        _isSimulatorTest = true;
        await CollectResult(runResult);

        if (Success != null && !Success.Value)
        {
            var pid = await GetPidFromRunLog();
            if (pid > 0)
            {
                await KillAppProcess(pid, _cancellationTokenSource);
            }
            else
            {
                _mainLog.WriteLine("Could not find pid in mtouch output.");
            }
        }
    }

    public async Task CollectDeviceResult(ProcessExecutionResult runResult)
    {
        _isSimulatorTest = false;
        await CollectResult(runResult);
    }

    private async Task<(string? ResultLine, bool Failed)> GetResultLine(string logPath)
    {
        string? resultLine = null;
        bool failed = false;
        using var reader = new StreamReader(logPath);
        string? line = null;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.Contains("Tests run:"))
            {
                Console.WriteLine(line);
                resultLine = line;
                break;
            }
            else if (line.Contains("[FAIL]"))
            {
                Console.WriteLine(line);
                failed = true;
            }
        }
        return (ResultLine: resultLine, Failed: failed);
    }

    private async Task<(string? resultLine, bool failed, bool crashed)> ParseResultFile(AppBundleInformation appInfo, string test_log_path, bool timed_out)
    {
        (string? resultLine, bool failed, bool crashed) parseResult = (null, false, false);
        if (!File.Exists(test_log_path))
        {
            parseResult.crashed = true; // if we do not have a log file, the test crashes
            return parseResult;
        }
        // parsing the result is different if we are in jenkins or not.
        // When in Jenkins, Touch.Unit produces an xml file instead of a console log (so that we can get better test reporting).
        // However, for our own reporting, we still want the console-based log. This log is embedded inside the xml produced
        // by Touch.Unit, so we need to extract it and write it to disk. We also need to re-save the xml output, since Touch.Unit
        // wraps the NUnit xml output with additional information, which we need to unwrap so that Jenkins understands it.
        //
        // On the other hand, the nunit and xunit do not have that data and have to be parsed.
        //
        // This if statement has a small trick, we found out that internet sharing in some of the bots (VSTS) does not work, in
        // that case, we cannot do a TCP connection to xharness to get the log, this is a problem since if we did not get the xml
        // from the TCP connection, we are going to fail when trying to read it and not parse it. Therefore, we are not only
        // going to check if we are in CI, but also if the listener_log is valid.
        var path = Path.ChangeExtension(test_log_path, "xml");
        if (path == test_log_path)
        {
            path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-clean.xml");
        }

        _resultParser.CleanXml(test_log_path, path);

        if (ResultsUseXml && _resultParser.IsValidXml(path, out var xmlType))
        {
            try
            {
                var newFilename = _resultParser.GetXmlFilePath(path, xmlType);

                // at this point, we have the test results, but we want to be able to have attachments in vsts, so if the format is
                // the right one (NUnitV3) add the nodes. ATM only TouchUnit uses V3.
                var testRunName = $"{appInfo.AppName} {appInfo.Variation}";
                if (xmlType == XmlResultJargon.NUnitV3)
                {
                    var logFiles = new List<string>();
                    // add our logs AND the logs of the previous task, which is the build task
                    logFiles.AddRange(Directory.GetFiles(_crashLogs.Directory));
                    if (_additionalLogsDirectory != null) // when using the run command, we do not have a build task, ergo, there are no logs to add.
                    {
                        logFiles.AddRange(Directory.GetFiles(_additionalLogsDirectory));
                    }
                    // add the attachments and write in the new filename
                    // add a final prefix to the file name to make sure that the VSTS test uploaded just pick
                    // the final version, else we will upload tests more than once
                    newFilename = XmlResultParser.GetVSTSFilename(newFilename);
                    _resultParser.UpdateMissingData(path, newFilename, testRunName, logFiles);
                }
                else
                {
                    // rename the path to the correct value
                    File.Move(path, newFilename);
                }
                path = newFilename;

                if (_generateHtml)
                {
                    // write the human readable results in a tmp file, which we later use to step on the logs
                    var humanReadableLog = _logs.CreateFile(Path.GetFileNameWithoutExtension(test_log_path) + ".log", LogType.NUnitResult);
                    (parseResult.resultLine, parseResult.failed) = _resultParser.ParseResults(path, xmlType, humanReadableLog);
                }
                else
                {
                    (parseResult.resultLine, parseResult.failed) = _resultParser.ParseResults(path, xmlType, (StreamWriter?)null);
                }

                // we do not longer need the tmp file
                _logs.AddFile(path, LogType.XmlLog.ToString());
                return parseResult;

            }
            catch (Exception e)
            {
                _mainLog.WriteLine("Could not parse xml result file: {0}", e);
                // print file for better debugging
                _mainLog.WriteLine("File data is:");
                _mainLog.WriteLine(new string('#', 10));
                using (var stream = new StreamReader(path))
                {
                    string? line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        _mainLog.WriteLine(line);
                    }
                }
                _mainLog.WriteLine(new string('#', 10));
                _mainLog.WriteLine("End of xml results.");
                if (timed_out)
                {
                    WrenchLog.WriteLine($"AddSummary: <b><i>{_runMode} timed out</i></b><br/>");
                    return parseResult;
                }
                else
                {
                    WrenchLog.WriteLine($"AddSummary: <b><i>{_runMode} crashed</i></b><br/>");
                    _mainLog.WriteLine("Test run crashed");
                    parseResult.crashed = true;
                    return parseResult;
                }
            }

        }
        // delete not needed copy
        File.Delete(path);

        // not the most efficient way but this just happens when we run
        // the tests locally and we usually do not run all tests, we are
        // more interested to be efficent on the bots
        (parseResult.resultLine, parseResult.failed) = await GetResultLine(test_log_path);
        return parseResult;
    }

    private async Task<(bool Succeeded, bool Crashed, string ResultLine)> TestsSucceeded(AppBundleInformation appInfo, string test_log_path, bool timed_out)
    {
        var (resultLine, failed, crashed) = await ParseResultFile(appInfo, test_log_path, timed_out);
        // read the parsed logs in a human readable way
        if (resultLine != null)
        {
            var tests_run = resultLine.Replace("Tests run: ", "");
            if (failed)
            {
                WrenchLog.WriteLine("AddSummary: <b>{0} failed: {1}</b><br/>", _runMode, tests_run);
                _mainLog.WriteLine("Test run failed");
                return (false, crashed, resultLine);
            }
            else
            {
                WrenchLog.WriteLine("AddSummary: {0} succeeded: {1}<br/>", _runMode, tests_run);
                _mainLog.WriteLine("Test run succeeded");
                return (true, crashed, resultLine);
            }
        }
        else if (timed_out)
        {
            WrenchLog.WriteLine("AddSummary: <b><i>{0} timed out</i></b><br/>", _runMode);
            _mainLog.WriteLine("Test run timed out");
            return (false, false, "Test run timed out");
        }
        else
        {
            WrenchLog.WriteLine("AddSummary: <b><i>{0} crashed</i></b><br/>", _runMode);
            _mainLog.WriteLine("Test run crashed");
            return (false, true, "Test run crashed");
        }
    }

    /// <summary>
    /// Generate all the xml failures that will help the integration with the CI and return the failure reason
    /// </summary>
    private async Task GenerateXmlFailures(string failure, bool crashed, string? crashReason)
    {
        if (!ResultsUseXml) // nothing to do
        {
            return;
        }

        if (!string.IsNullOrEmpty(crashReason))
        {
            _resultParser.GenerateFailure(
                _logs,
                "crash",
                _appInfo.AppName,
                _appInfo.Variation,
                $"App Crash {_appInfo.AppName} {_appInfo.Variation}",
                $"App crashed: {failure}",
                _mainLog.FullPath,
                _xmlJargon);

            return;
        }

        if (_launchFailure)
        {
            _resultParser.GenerateFailure(
                _logs,
                "launch",
                _appInfo.AppName,
                _appInfo.Variation,
                $"App Launch {_appInfo.AppName} {_appInfo.Variation} on {_deviceName}",
                $"{failure} on {_deviceName}",
                _mainLog.FullPath,
                _xmlJargon);

            return;
        }

        if (!_isSimulatorTest && crashed && string.IsNullOrEmpty(crashReason))
        {
            // this happens more that what we would like on devices, the main reason most of the time is that we have had netwoking problems and the
            // tcp connection could not be stablished. We are going to report it as an error since we have not parsed the logs, evne when the app might have
            // not crashed. We need to check the main_log to see if we do have an tcp issue or not
            if (await TcpConnectionFailed())
            {
                _resultParser.GenerateFailure(
                    _logs,
                    "tcp-connection",
                    _appInfo.AppName,
                    _appInfo.Variation,
                    $"TcpConnection on {_deviceName}",
                    $"Device {_deviceName} could not reach the host over tcp.",
                    _mainLog.FullPath,
                    _xmlJargon);
            }
        }
        else if (_timedout)
        {
            _resultParser.GenerateFailure(
                _logs,
                "timeout",
                _appInfo.AppName,
                _appInfo.Variation,
                $"App Timeout {_appInfo.AppName} {_appInfo.Variation} on bot {_deviceName}",
                $"{_appInfo.AppName} {_appInfo.Variation} Test run timed out after {_timeout.TotalMinutes} minute(s) on bot {_deviceName}.",
                _mainLog.FullPath,
                _xmlJargon);
        }
    }

    public async Task<(TestExecutingResult ExecutingResult, string? ResultMessage)> ParseResult()
    {
        (TestExecutingResult ExecutingResult, string? ResultMessage) result = (ExecutingResult: TestExecutingResult.Finished, ResultMessage: null);
        var crashed = false;
        if (File.Exists(_listener.TestLog.FullPath))
        {
            WrenchLog.WriteLine("AddFile: {0}", _listener.TestLog.FullPath);
            (Success, crashed, result.ResultMessage) = await TestsSucceeded(_appInfo, _listener.TestLog.FullPath, _timedout);
        }
        else if (_timedout)
        {
            WrenchLog.WriteLine("AddSummary: <b><i>{0} never launched</i></b><br/>", _runMode);
            _mainLog.WriteLine("Test run never launched");
            result.ResultMessage = "Test runner never started";
            Success = false;
        }
        else if (_launchFailure)
        {
            WrenchLog.WriteLine("AddSummary: <b><i>{0} failed to launch</i></b><br/>", _runMode);
            _mainLog.WriteLine("Test run failed to launch");
            result.ResultMessage = "Test runner failed to launch";
            Success = false;
        }
        else
        {
            WrenchLog.WriteLine("AddSummary: <b><i>{0} crashed at startup (no log)</i></b><br/>", _runMode);
            _mainLog.WriteLine("Test run started but crashed and no test results were reported");
            result.ResultMessage = "No test log file was produced";
            crashed = true;
            Success = false;
        }

        if (!Success.HasValue)
        {
            Success = false;
        }

        var crashLogWaitTime = 0;
        if (!Success.Value)
        {
            crashLogWaitTime = 5;
        }

        if (crashed)
        {
            crashLogWaitTime = 30;
        }

        await _crashReporter.EndCaptureAsync(TimeSpan.FromSeconds(crashLogWaitTime));

        if (_timedout)
        {
            if (TestExecutionStarted)
            {
                result.ExecutingResult = TestExecutingResult.TimedOut;
            }
            else
            {
                result.ExecutingResult = TestExecutingResult.LaunchTimedOut;
            }
        }
        else if (_launchFailure)
        {
            result.ExecutingResult = TestExecutingResult.LaunchFailure;
        }
        else if (crashed)
        {
            result.ExecutingResult = TestExecutingResult.Crashed;
        }
        else if (Success.Value)
        {
            result.ExecutingResult = TestExecutingResult.Succeeded;
        }
        else
        {
            result.ExecutingResult = TestExecutingResult.Failed;
        }

        // Check crash reports to see if any of them explains why the test run crashed.
        if (!Success.Value)
        {
            int pid = -1;
            string? crashReason = null;
            foreach (var crashLog in _crashLogs)
            {
                try
                {
                    _logs.Add(crashLog);

                    if (pid == -1)
                    {
                        // Find the pid
                        pid = await GetPidFromMainLog();
                    }

                    GetCrashReason(pid, crashLog, out crashReason);
                    if (crashReason != null)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    var message = string.Format("Failed to process crash report '{1}': {0}", e.Message, crashLog.Description);
                    _mainLog.WriteLine(message);
                    _exceptionLogger?.Invoke(2, message);
                }
            }

            if (!string.IsNullOrEmpty(crashReason))
            {
                if (crashReason == "per-process-limit")
                {
                    result.ResultMessage = "Killed due to using too much memory (per-process-limit).";
                }
                else
                {
                    result.ResultMessage = $"Killed by the OS ({crashReason})";
                }
            }
            else if (_launchFailure)
            {
                // same as with a crash
                result.ResultMessage = $"Launch failure";
            }

            await GenerateXmlFailures(result.ResultMessage, crashed, crashReason);
        }

        return result;
    }

    public void Dispose()
    {
        _crashLogs.Dispose();
        GC.SuppressFinalize(this);
    }
}

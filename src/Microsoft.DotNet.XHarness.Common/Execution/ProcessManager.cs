// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public abstract class ProcessManager : IProcessManager
{
    #region Abstract methods

    protected abstract int Kill(int pid, int sig);
    protected abstract List<int> GetChildProcessIds(ILog log, int pid);

    #endregion

    #region IProcessManager implementation

    public async Task<ProcessExecutionResult> ExecuteCommandAsync(string filename,
        IList<string> args,
        ILog log,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null)
        => await ExecuteCommandAsync(filename, args, log, log, log, timeout, environmentVariables, cancellationToken);

    public async Task<ProcessExecutionResult> ExecuteCommandAsync(string filename,
        IList<string> args,
        ILog log,
        ILog stdout,
        ILog stderr,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null)
    {
        using var p = new Process();
        p.StartInfo.FileName = filename ?? throw new ArgumentNullException(nameof(filename));
        p.StartInfo.Arguments = StringUtils.FormatArguments(args);
        return await RunAsync(p, log, stdout, stderr, timeout, environmentVariables, cancellationToken);
    }

    public Task<ProcessExecutionResult> RunAsync(
        Process process,
        ILog log,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null)
        => RunAsync(process, log, log, log, timeout, environmentVariables, cancellationToken, diagnostics);

    public Task<ProcessExecutionResult> RunAsync(
        Process process,
        ILog log,
        ILog stdout,
        ILog stderr,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null)
        => RunAsyncInternal(process, log, stdout, stderr, timeout, environmentVariables, cancellationToken, diagnostics);

    public Task KillTreeAsync(Process process, ILog log, bool? diagnostics = true) => KillTreeAsync(process.Id, log, diagnostics);

    public Task KillTreeAsync(int pid, ILog log, bool? diagnostics = true) => KillTreeAsync(pid, log, (pid, signal) => Kill(pid, signal), (log, pid) => GetChildProcessIds(log, pid), diagnostics);

    protected static async Task KillTreeAsync(
        int pid,
        ILog log,
        Action<int, int> kill,
        Func<ILog, int, IList<int>> getChildProcessIds,
        bool? diagnostics = true)
    {
        log.WriteLine($"Killing process tree of {pid}...");

        var pids = getChildProcessIds(log, pid);
        log.WriteLine($"Pids to kill: {string.Join(", ", pids.Select((v) => v.ToString()).ToArray())}");

        if (diagnostics == true)
        {
            foreach (var pidToDiagnose in pids)
            {
                log.WriteLine($"Running lldb diagnostics for pid {pidToDiagnose}");

                var template = Path.GetTempFileName();
                try
                {
                    var commands = new StringBuilder();
                    using (var dbg = new Process())
                    {
                        commands.AppendLine($"process attach --pid {pidToDiagnose}");
                        commands.AppendLine("thread list");
                        commands.AppendLine("thread backtrace all");
                        commands.AppendLine("detach");
                        commands.AppendLine("quit");

                        dbg.StartInfo.FileName = "lldb";
                        dbg.StartInfo.Arguments = StringUtils.FormatArguments("--source", template);

                        File.WriteAllText(template, commands.ToString());

                        log.WriteLine($"Printing backtrace for pid={pidToDiagnose}");
                        await RunAsyncInternal(
                            process: dbg,
                            log: new NullLog(),
                            stdout: log,
                            stderr: log,
                            kill,
                            getChildProcessIds,
                            timeout: TimeSpan.FromSeconds(20),
                            diagnostics: false);
                    }
                }
                catch (Win32Exception e) when (e.NativeErrorCode == 2)
                {
                    log.WriteLine("lldb was not found, skipping diagnosis..");
                }
                catch (Exception e)
                {
                    log.WriteLine("Failed to diagnose the process using lldb:" + Environment.NewLine + e);
                }
                finally
                {
                    try
                    {
                        File.Delete(template);
                    }
                    catch
                    {
                        // Don't care
                    }
                }
            }
        }

        // Send SIGABRT since that produces a crash report
        // lldb may fail to attach to system processes, but crash reports will still be produced with potentially helpful stack traces.
        for (int i = 0; i < pids.Count; i++)
        {
            kill(pids[i], 6);
        }

        // send kill -9 anyway as a last resort
        for (int i = 0; i < pids.Count; i++)
        {
            kill(pids[i], 9);
        }
    }

    protected Task<ProcessExecutionResult> RunAsyncInternal(
        Process process,
        ILog log,
        ILog stdout,
        ILog stderr,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null) => RunAsyncInternal(
            process,
            log,
            stdout,
            stderr,
            (pid, signal) => Kill(pid, signal),
            (log, pid) => GetChildProcessIds(log, pid),
            timeout,
            environmentVariables,
            cancellationToken,
            diagnostics);

    protected static async Task<ProcessExecutionResult> RunAsyncInternal(
        Process process,
        ILog log,
        ILog stdout,
        ILog stderr,
        Action<int, int> kill,
        Func<ILog, int, IList<int>> getChildProcessIds,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null)
    {
        var stdoutCompletion = new TaskCompletionSource<bool>();
        var stderrCompletion = new TaskCompletionSource<bool>();
        var result = new ProcessExecutionResult();

        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        // Make cute emojiis show up as cute emojiis in the output instead of ugly text symbols!
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        process.StartInfo.UseShellExecute = false;

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                if (kvp.Value == null)
                {
                    process.StartInfo.EnvironmentVariables.Remove(kvp.Key);
                }
                else
                {
                    process.StartInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }
        }

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (stdout)
                {
                    stdout.WriteLine(e.Data);
                    stdout.Flush();
                }
            }
            else
            {
                stdoutCompletion.TrySetResult(true);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                lock (stderr)
                {
                    stderr.WriteLine(e.Data);
                    stderr.Flush();
                }
            }
            else
            {
                stderrCompletion.TrySetResult(true);
            }
        };

        var sb = new StringBuilder();
        sb.Append($"Running {StringUtils.Quote(process.StartInfo.FileName)} {process.StartInfo.Arguments}");

        if (process.StartInfo.EnvironmentVariables != null)
        {
            var currentEnvironment = ToDictionary(Environment.GetEnvironmentVariables());
            var processEnvironment = ToDictionary(process.StartInfo.EnvironmentVariables);
            var allVariables = currentEnvironment.Keys.Union(processEnvironment.Keys).Distinct();

            bool headerShown = false;
            foreach (var variable in allVariables)
            {
                if (variable == null)
                {
                    continue;
                }

                currentEnvironment.TryGetValue(variable, out var a);
                processEnvironment.TryGetValue(variable, out var b);

                if (a != b)
                {
                    if (!headerShown)
                    {
                        sb.AppendLine().Append("With env vars: ");
                        headerShown = true;
                    }

                    sb.AppendLine().Append($"    {variable} = '{StringUtils.Quote(b)}'");
                }
            }
        }

        // Separate process calls in logs
        log.WriteLine(string.Empty);
        log.WriteLine(sb.ToString());

        process.Start();
        var pid = process.Id;

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        cancellationToken?.Register(() =>
        {
            var hasExited = false;
            try
            {
                hasExited = process.HasExited;
            }
            catch
            {
                // Process.HasExited can sometimes throw exceptions, so
                // just ignore those and to be safe treat it as the
                // process didn't exit (the safe option being to not leave
                // processes behind).
            }

            if (!hasExited)
            {
                stderr.WriteLine($"Killing process {pid} as it was cancelled");
                kill(pid, 9);
            }
        });

        if (timeout.HasValue)
        {
            if (!await WaitForExitAsync(process, timeout.Value))
            {
                log.WriteLine($"Process {pid} didn't exit within {timeout} and will be killed");

                await KillTreeAsync(pid, log, kill, getChildProcessIds, diagnostics ?? true);

                result.TimedOut = true;

                lock (stderr)
                {
                    log.WriteLine($"{pid} Execution timed out after {timeout.Value.TotalSeconds} seconds and the process was killed.");
                }
            }
        }
        else
        {
            await WaitForExitAsync(process);
        }

        if (process.HasExited)
        {
            // make sure redirected output events are finished
            process.WaitForExit();
        }

        Task.WaitAll(new Task[] { stderrCompletion.Task, stdoutCompletion.Task }, TimeSpan.FromSeconds(1));

        try
        {
            result.ExitCode = process.ExitCode;
            log.WriteLine($"Process {Path.GetFileName(process.StartInfo.FileName)} exited with {result.ExitCode}");
        }
        catch (Exception e)
        {
            result.ExitCode = 12345678;
            log.WriteLine($"Failed to get ExitCode: {e}");
        }

        return result;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan? timeout = null)
    {
        if (process.HasExited)
        {
            return true;
        }

        var tcs = new TaskCompletionSource<bool>();

        void ProcessExited(object? sender, EventArgs ea)
        {
            process.Exited -= ProcessExited;
            tcs.TrySetResult(true);
        }

        process.Exited += ProcessExited;
        process.EnableRaisingEvents = true;

        // Check if process exited again, in case it exited after we checked
        // the last time, but before we attached the event handler.
        if (process.HasExited)
        {
            process.Exited -= ProcessExited;
            tcs.TrySetResult(true);
            return true;
        }

        if (timeout.HasValue)
        {
            return await tcs.Task.TimeoutAfter(timeout.Value);
        }
        else
        {
            await tcs.Task;
            return true;
        }
    }

    private static Dictionary<string, string?> ToDictionary(IEnumerable enumerable) =>
        enumerable.Cast<DictionaryEntry>().ToDictionary(v => v.Key.ToString()!, v => v.Value?.ToString(), StringComparer.Ordinal);

    #endregion
}

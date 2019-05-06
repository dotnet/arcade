// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.VersionTools.Util
{
    internal class Command
    {
        public static readonly string[] RunnableSuffixes = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new string[] { ".exe", ".cmd", ".bat" }
            : new string[] { string.Empty };

        private Process _process;

        private Action<string> _statusForward;

        private StringWriter _stdOutCapture;
        private StringWriter _stdErrCapture;

        private Action<string> _stdOutForward;
        private Action<string> _stdErrForward;

        private Action<string> _stdOutHandler;
        private Action<string> _stdErrHandler;

        private bool _running = false;
        private bool _quietBuildReporter = false;

        private Command(string executable, string args)
        {
            // Set the things we need
            var psi = new ProcessStartInfo()
            {
                FileName = executable,
                Arguments = args
            };

            _process = new Process()
            {
                StartInfo = psi
            };
        }

        public static Command Create(string executable, params string[] args)
        {
            return Create(executable, ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args));
        }

        public static Command Create(string executable, IEnumerable<string> args)
        {
            return Create(executable, ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args));
        }

        public static Command Create(string executable, string args)
        {
            ResolveExecutablePath(ref executable, ref args);

            return new Command(executable, args);
        }

        private static void ResolveExecutablePath(ref string executable, ref string args)
        {
            // On Windows, we want to avoid using "cmd" if possible (it mangles the colors, and a bunch of other things)
            // So, do a quick path search to see if we can just directly invoke it
            var useCmd = ShouldUseCmd(executable);

            if (useCmd)
            {
                var comSpec = System.Environment.GetEnvironmentVariable("ComSpec");

                // cmd doesn't like "foo.exe ", so we need to ensure that if
                // args is empty, we just run "foo.exe"
                if (!string.IsNullOrEmpty(args))
                {
                    executable = (executable + " " + args).Replace("\"", "\\\"");
                }
                args = $"/C \"{executable}\"";
                executable = comSpec;
            }
        }

        private static bool ShouldUseCmd(string executable)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var extension = Path.GetExtension(executable);
                if (!string.IsNullOrEmpty(extension))
                {
                    return !string.Equals(extension, ".exe", StringComparison.Ordinal);
                }
                else if (executable.Contains(Path.DirectorySeparatorChar))
                {
                    // It's a relative path without an extension
                    if (File.Exists(executable + ".exe"))
                    {
                        // It refers to an exe!
                        return false;
                    }
                }
                else
                {
                    // Search the path to see if we can find it 
                    foreach (var path in System.Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator))
                    {
                        var candidate = Path.Combine(path, executable + ".exe");
                        if (File.Exists(candidate))
                        {
                            // We found an exe!
                            return false;
                        }
                    }
                }

                // It's a non-exe :(
                return true;
            }

            // Non-windows never uses cmd
            return false;
        }

        public Command QuietBuildReporter()
        {
            _quietBuildReporter = true;
            return this;
        }

        public CommandResult Execute()
        {
            ThrowIfRunning();
            _running = true;

            if (_process.StartInfo.RedirectStandardOutput)
            {
                _process.OutputDataReceived += (sender, args) =>
                {
                    ProcessData(args.Data, _stdOutCapture, _stdOutForward, _stdOutHandler);
                };
            }

            if (_process.StartInfo.RedirectStandardError)
            {
                _process.ErrorDataReceived += (sender, args) =>
                {
                    ProcessData(args.Data, _stdErrCapture, _stdErrForward, _stdErrHandler);
                };
            }

            _process.EnableRaisingEvents = true;

            if (_process.StartInfo.RedirectStandardOutput ||
                _process.StartInfo.RedirectStandardInput ||
                _process.StartInfo.RedirectStandardError)
            {
                _process.StartInfo.UseShellExecute = false;
            }

            var sw = Stopwatch.StartNew();
            ReportExecBegin();

            _process.Start();

            if (_process.StartInfo.RedirectStandardOutput)
            {
                _process.BeginOutputReadLine();
            }

            if (_process.StartInfo.RedirectStandardError)
            {
                _process.BeginErrorReadLine();
            }

            _process.WaitForExit();

            var exitCode = _process.ExitCode;

            ReportExecEnd(exitCode);

            return new CommandResult(
                _process.StartInfo,
                exitCode,
                _stdOutCapture?.GetStringBuilder()?.ToString(),
                _stdErrCapture?.GetStringBuilder()?.ToString());
        }

        public Command WorkingDirectory(string projectDirectory)
        {
            _process.StartInfo.WorkingDirectory = projectDirectory;
            return this;
        }

        public Command EnvironmentVariable(string name, string value)
        {
#if NET45
            _process.StartInfo.EnvironmentVariables[name] = value;
#else
            _process.StartInfo.Environment[name] = value;
#endif
            _process.StartInfo.UseShellExecute = false;
            return this;
        }

        public Command ForwardStatus(TextWriter to = null)
        {
            ThrowIfRunning();
            if (to == null)
            {
                _statusForward = Console.WriteLine;
            }
            else
            {
                _statusForward = to.WriteLine;
            }
            return this;
        }

        public Command CaptureStdOut()
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardOutput = true;
            _stdOutCapture = new StringWriter();
            return this;
        }

        public Command CaptureStdErr()
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardError = true;
            _stdErrCapture = new StringWriter();
            return this;
        }

        public Command ForwardStdOut(TextWriter to = null)
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardOutput = true;
            if (to == null)
            {
                _stdOutForward = Console.WriteLine;
            }
            else
            {
                _stdOutForward = to.WriteLine;
            }
            return this;
        }

        public Command ForwardStdErr(TextWriter to = null)
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardError = true;
            if (to == null)
            {
                _stdErrForward = Console.WriteLine;
            }
            else
            {
                _stdErrForward = to.WriteLine;
            }
            return this;
        }

        public Command OnOutputLine(Action<string> handler)
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardOutput = true;
            if (_stdOutHandler != null)
            {
                throw new InvalidOperationException("Already handling stdout!");
            }
            _stdOutHandler = handler;
            return this;
        }

        public Command OnErrorLine(Action<string> handler)
        {
            ThrowIfRunning();
            _process.StartInfo.RedirectStandardError = true;
            if (_stdErrHandler != null)
            {
                throw new InvalidOperationException("Already handling stderr!");
            }
            _stdErrHandler = handler;
            return this;
        }

        private string FormatProcessInfo(ProcessStartInfo info, bool includeWorkingDirectory)
        {
            string prefix = includeWorkingDirectory ?
                $"{info.WorkingDirectory}> {info.FileName}" :
                info.FileName;

            if (string.IsNullOrWhiteSpace(info.Arguments))
            {
                return prefix;
            }

            return prefix + " " + info.Arguments;
        }

        private void ReportExecBegin()
        {
            if (!_quietBuildReporter && _statusForward != null)
            {
                _statusForward($"[EXEC Begin] {FormatProcessInfo(_process.StartInfo, includeWorkingDirectory: false)}");
            }
        }

        private void ReportExecEnd(int exitCode)
        {
            if (!_quietBuildReporter && _statusForward != null)
            {
                bool success = exitCode == 0;

                var message = $"{FormatProcessInfo(_process.StartInfo, includeWorkingDirectory: !success)} exited with {exitCode}";

                _statusForward($"[EXEC End] {message}");
            }
        }

        private void ThrowIfRunning([CallerMemberName] string memberName = null)
        {
            if (_running)
            {
                throw new InvalidOperationException($"Unable to invoke {memberName} after the command has been run");
            }
        }

        private void ProcessData(string data, StringWriter capture, Action<string> forward, Action<string> handler)
        {
            if (data == null)
            {
                return;
            }

            if (capture != null)
            {
                capture.WriteLine(data);
            }

            if (forward != null)
            {
                forward(data);
            }

            if (handler != null)
            {
                handler(data);
            }
        }
    }
}

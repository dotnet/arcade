// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public enum ScriptType
    {
        PowerShell,
        Shell
    }

    public class ScriptRunner
    {
        private readonly string _testDirectory;

        public ScriptRunner(string testDirectory)
        {
            _testDirectory = testDirectory;
        }

        public async Task<(int exitCode, string output, string error)> RunPowerShellScript(string configFilePath, string password = null)
        {
            var scriptPath = Path.Combine(GetRepoRoot(), "eng", "common", "SetupNugetSources.ps1");
            var arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -ConfigFile \"{configFilePath}\"";
            
            if (!string.IsNullOrEmpty(password))
            {
                arguments += $" -Password \"{password}\"";
            }

            return await RunProcess("powershell.exe", arguments);
        }

        public async Task<(int exitCode, string output, string error)> RunShellScript(string configFilePath, string credToken = null)
        {
            var scriptPath = Path.Combine(GetRepoRoot(), "eng", "common", "SetupNugetSources.sh");
            
            // Make script executable if on Unix
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await RunProcess("chmod", $"+x \"{scriptPath}\"");
            }

            var arguments = $"\"{scriptPath}\" \"{configFilePath}\"";
            if (!string.IsNullOrEmpty(credToken))
            {
                arguments += $" \"{credToken}\"";
            }

            var shell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bash.exe" : "/bin/bash";
            return await RunProcess(shell, arguments);
        }

        private async Task<(int exitCode, string output, string error)> RunProcess(string fileName, string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit());

            return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
        }

        public async Task<(int exitCode, string output, string error)> RunScript(string configFilePath, string credential = null)
        {
            var scriptType = GetPlatformAppropriateScriptType();
            switch (scriptType)
            {
                case ScriptType.PowerShell:
                    return await RunPowerShellScript(configFilePath, credential);
                case ScriptType.Shell:
                    return await RunShellScript(configFilePath, credential);
                default:
                    throw new ArgumentException($"Unsupported script type: {scriptType}");
            }
        }

        public static ScriptType GetPlatformAppropriateScriptType()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ScriptType.PowerShell : ScriptType.Shell;
        }

        private static string GetRepoRoot()
        {
            var current = Directory.GetCurrentDirectory();
            while (current != null && !File.Exists(Path.Combine(current, "global.json")))
            {
                current = Directory.GetParent(current)?.FullName;
            }
            return current ?? throw new InvalidOperationException("Could not find repository root with global.json");
        }
    }
}


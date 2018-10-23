// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.DarcLib.Helpers
{
    internal static class LocalHelpers
    {
        public static string GetEditorPath(ILogger logger)
        {
            string editor = ExecuteCommand("git", "config --get core.editor", logger);

            // If there is nothing set in core.editor we try to default it to notepad if running in Windows, if not default it to
            // vim
            if (string.IsNullOrEmpty(editor))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    editor = ExecuteCommand("where", "notepad", logger);
                }
                else
                {
                    editor = ExecuteCommand("which", "vim", logger);
                }
            }

            return editor;
        }

        public static string GetGitDir(ILogger logger)
        {
            string dir = ExecuteCommand("git", "rev-parse --absolute-git-dir", logger);

            if (string.IsNullOrEmpty(dir))
            {
                throw new Exception("'.git' directory was not found. Check if git is installed and that a .git directory exists in the root of your repository.");
            }

            return dir;
        }

        public static string ExecuteCommand(string fileName, string arguments, ILogger logger)
        {
            string output = null;

            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    FileName = fileName,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                using (Process process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.StartInfo.Arguments = arguments;
                    process.Start();

                    output = process.StandardOutput.ReadToEnd().Trim();

                    process.WaitForExit();
                }

                if (string.IsNullOrEmpty(output))
                {
                    logger.LogError($"There was an error while running git.exe {arguments}");
                }
            }
            catch (Exception exc)
            {
                logger.LogWarning($"Something failed while trying to execute '{fileName} {arguments}'. Exception: {exc.Message}");
            }

            return output;
        }
    }
}

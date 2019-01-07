// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.DarcLib.Helpers
{
    public static class LocalHelpers
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

            // Split this by newline in case where are multiple paths;
            int newlineIndex = editor.IndexOf(System.Environment.NewLine);
            if (newlineIndex != -1)
            {
                editor = editor.Substring(0, newlineIndex);
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

        public static string GitShow(string repoFolderPath, string commit, string fileName, ILogger logger)
        {
            string fileContents = ExecuteCommand("git", $"show {commit}:{fileName}", logger, repoFolderPath);

            if (string.IsNullOrEmpty(fileContents))
            {
                throw new Exception($"Could not show the contents of '{fileName}' at '{commit}' in '{repoFolderPath}'...");
            }

            return fileContents;
        }

        /// <summary>
        /// For each child folder in the provided "source" folder we check for the existance of a given commit. Each folder in "source"
        /// represent a different repo.
        /// </summary>
        /// <param name="sourceFolder">The main source folder.</param>
        /// <param name="commit">The commit to search for in a repo folder.</param>
        /// <param name="logger">The logger.</param>
        /// <returns></returns>
        public static string GetRepoPathFromFolder(string sourceFolder, string commit, ILogger logger)
        {
            foreach (string directory in Directory.GetDirectories(sourceFolder))
            {
                string containsCommand = ExecuteCommand("git", $"branch --contains {commit}", logger, directory);

                if (!string.IsNullOrEmpty(containsCommand))
                {
                    return directory;
                }
            }

            return null;
        }

        public static string ExecuteCommand(string fileName, string arguments, ILogger logger, string workingDirectory = null)
        {
            string output = null;

            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = fileName,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
                };

                using (Process process = new Process())
                {
                    process.StartInfo = processInfo;
                    process.StartInfo.Arguments = arguments;
                    process.Start();

                    output = process.StandardOutput.ReadToEnd().Trim();

                    process.WaitForExit();
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

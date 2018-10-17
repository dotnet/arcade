// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Darc.Helpers
{
    internal static class LocalCommands
    {
        /// <summary>
        /// Retrieve the settings from the combination of the command line
        /// options and the user's darc settings file. If repoUri is set, we define the
        /// repo type and read the required token field from the settings file
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Darc settings for use in remote commands</returns>
        /// <remarks>The command line takes precedence over the darc settings file.</remarks>
        public static DarcSettings GetSettings(CommandLineOptions options, ILogger logger, string repoUri = null)
        {
            DarcSettings darcSettings = new DarcSettings();
            darcSettings.GitType = GitRepoType.None;

            try
            {
                LocalSettings localSettings = LocalSettings.LoadSettings();
                darcSettings.BuildAssetRegistryBaseUri = localSettings.BuildAssetRegistryBaseUri;
                darcSettings.BuildAssetRegistryPassword = localSettings.BuildAssetRegistryPassword;

                if (!string.IsNullOrEmpty(repoUri))
                {
                    if (repoUri.Contains("github"))
                    {
                        darcSettings.GitType = GitRepoType.GitHub;
                        darcSettings.PersonalAccessToken = localSettings.GitHubToken;
                    }
                    else
                    {
                        darcSettings.GitType = GitRepoType.AzureDevOps;
                        darcSettings.PersonalAccessToken = localSettings.AzureDevOpsToken;
                    }
                }
             }
            catch (FileNotFoundException)
            {
                // Doesn't have a settings file, which is not an error
            }
            catch (Exception e)
            {
                logger.LogWarning(e, $"Failed to load the darc settings file, may be corrupted");
            }

            // Override if non-empty on command line
            darcSettings.BuildAssetRegistryBaseUri = OverrideIfSet(darcSettings.BuildAssetRegistryBaseUri,
                                                                   options.BuildAssetRegistryBaseUri);
            darcSettings.BuildAssetRegistryPassword = OverrideIfSet(darcSettings.BuildAssetRegistryPassword, 
                                                                    options.BuildAssetRegistryPassword);

            // Currently the darc settings only has one PAT type which is interpreted differently based
            // on the git type (Azure DevOps vs. GitHub).  For now, leave this setting empty until
            // we know what we are talking to.

            return darcSettings;
        }

        public static string GetEditorPath(ILogger logger)
        {
            string editor = ExecuteCommand("git.exe", "config --get core.editor", logger);

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
            string dir = ExecuteCommand("git.exe", "rev-parse --absolute-git-dir", logger);

            if (string.IsNullOrEmpty(dir))
            {
                throw new Exception("'.git' directory was not found. Check if git is installed and that a .git directory exists in the root of your repository.");
            }

            return dir;
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
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        return string.Empty;
                    }

                    output = process.StandardOutput.ReadToEnd().Trim();

                    // Workaround. With some git commands the non-error output is logged in the
                    // StandardError and not in StandardOutput. If the error code is 0, we also
                    // check the StandardError if output is null
                    if (string.IsNullOrEmpty(output))
                    {
                        output = process.StandardError.ReadToEnd().Trim();
                    }
                }

                string[] paths = output.Split(Environment.NewLine);

                output = paths[0];
            }
            catch (Exception exc)
            {
                logger.LogWarning($"Something failed while trying to execute '{fileName} {arguments}'. Exception: {exc.Message}");
            }

            return output;
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
                string containsCommand = ExecuteCommand("git.exe", $"branch --contains {commit}", logger, directory);

                if (!string.IsNullOrEmpty(containsCommand))
                {
                    return directory;
                }
            }

            return string.Empty;
        }

        public static string Show(string repoFolderPath, string commit, string fileName, ILogger logger)
        {
            string fileContents = ExecuteCommand("git.exe", $"show {commit}:{fileName}", logger, repoFolderPath);

            if (string.IsNullOrEmpty(fileContents))
            {
                throw new Exception($"Could not show the contents of '{fileName}' at '{commit}' in '{repoFolderPath}'...");
            }

            return fileContents;
        }

        private static string OverrideIfSet(string currentSetting, string commandLineSetting)
        {
            return !string.IsNullOrEmpty(commandLineSetting) ? commandLineSetting : currentSetting;
        }
    }
}

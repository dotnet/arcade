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
        /// options and the user's darc settings file.
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Darc settings for use in remote commands</returns>
        /// <remarks>The command line takes precedence over the darc settings file.</remarks>
        public static DarcSettings GetSettings(CommandLineOptions options, ILogger logger)
        {
            DarcSettings darcSettings = new DarcSettings();
            darcSettings.GitType = GitRepoType.None;

            try
            {
                LocalSettings localSettings = LocalSettings.LoadSettings();
                darcSettings.BuildAssetRegistryBaseUri = localSettings.BuildAssetRegistryBaseUri;
                darcSettings.BuildAssetRegistryPassword = localSettings.BuildAssetRegistryPassword;
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

        private static string OverrideIfSet(string currentSetting, string commandLineSetting)
        {
            return !string.IsNullOrEmpty(commandLineSetting) ? commandLineSetting : currentSetting;
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
                dir = Constants.DarcDirectory;

                Directory.CreateDirectory(dir);
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

                string[] paths = output.Split(Environment.NewLine);

                output = paths[0];
            }
            catch (Exception exc)
            {
                logger.LogWarning($"Something failed while trying to execute '{fileName} {arguments}'. Exception: {exc.Message}");
            }

            return output;
        }
    }
}

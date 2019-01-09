// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Operations;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Helpers
{
    /// <summary>
    /// Reads and writes the settings file
    /// </summary>
    internal class LocalSettings
    {
        private static string _defaultBuildAssetRegistryBaseUri = "https://maestro-prod.westus2.cloudapp.azure.com/";

        public string BuildAssetRegistryPassword { get; set; }

        public string GitHubToken { get; set; }

        public string AzureDevOpsToken { get; set; }

        public string BuildAssetRegistryBaseUri { get; set; } = _defaultBuildAssetRegistryBaseUri;

        /// <summary>
        /// Saves the settings in the settings files
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public int SaveSettingsFile(ILogger logger)
        {
            string settings = JsonConvert.SerializeObject(this);
            return EncodedFile.Create(Constants.SettingsFileName, settings, logger);
        }

        public static LocalSettings LoadSettingsFile()
        {
            string settings = EncodedFile.Read(Constants.SettingsFileName);
            return JsonConvert.DeserializeObject<LocalSettings>(settings);
        }

        public static LocalSettings LoadSettingsFile(CommandLineOptions options)
        {
            try
            {
                return LoadSettingsFile();
            }
            catch (Exception exc) when (exc is DirectoryNotFoundException || exc is FileNotFoundException)
            {
                if (string.IsNullOrEmpty(options.AzureDevOpsPat) &&
                    string.IsNullOrEmpty(options.GitHubPat) &&
                    string.IsNullOrEmpty(options.BuildAssetRegistryPassword))
                {
                    throw new DarcException("Please make sure to run darc authenticate and set" +
                        " 'bar_password' and 'github_token' or 'azure_devops_token' or append" +
                        "'-p <bar_password>' [--github-pat <github_token> | " +
                        "--azdev-pat <azure_devops_token>] to your command");
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieve the settings from the combination of the command line
        /// options and the user's darc settings file.
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Darc settings for use in remote commands</returns>
        /// <remarks>The command line takes precedence over the darc settings file.</remarks>
        public static DarcSettings GetDarcSettings(CommandLineOptions options, ILogger logger, string repoUri = null)
        {
            LocalSettings localSettings = null;
            DarcSettings darcSettings = new DarcSettings();
            darcSettings.GitType = GitRepoType.None;

            try
            {
                localSettings = LoadSettingsFile(options);

                if (localSettings != null)
                {
                    darcSettings.BuildAssetRegistryBaseUri = localSettings.BuildAssetRegistryBaseUri;
                    darcSettings.BuildAssetRegistryPassword = localSettings.BuildAssetRegistryPassword;
                }
                else
                {
                    darcSettings.BuildAssetRegistryBaseUri = _defaultBuildAssetRegistryBaseUri;
                    darcSettings.BuildAssetRegistryPassword = options.BuildAssetRegistryPassword;
                }

                if (!string.IsNullOrEmpty(repoUri))
                {
                    if (repoUri.Contains("github"))
                    {
                        darcSettings.GitType = GitRepoType.GitHub;
                        darcSettings.PersonalAccessToken = localSettings.GitHubToken;
                    }
                    else if (repoUri.Contains("dev.azure.com"))
                    {
                        darcSettings.GitType = GitRepoType.AzureDevOps;
                        darcSettings.PersonalAccessToken = localSettings.AzureDevOpsToken;
                    }
                    else
                    {
                        logger.LogWarning($"Unknown repository '{repoUri}'");
                    }
                }
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
    }
}

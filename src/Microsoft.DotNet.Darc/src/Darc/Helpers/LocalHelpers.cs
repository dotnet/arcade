// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using System;
using System.IO;

namespace Microsoft.DotNet.Darc.Helpers
{
    internal class Settings
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
    }
}

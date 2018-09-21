// Licensed to the .NET Foundation under one or more agreements.)
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.DotNet.Darc.Models
{
    internal class AuthenticateEditorPopUp : EditorPopUp
    {
        private readonly ILogger _logger;

        private const string barPasswordElement = "bar_password";
        private const string githubTokenElement = "github_token";
        private const string azureDevOpsTokenElement = "azure_devops_token";
        private const string barBaseUriElement = "build_asset_registry_base_uri";

        public AuthenticateEditorPopUp(string path, ILogger logger)
            : base(path)
        {
            _logger = logger;
            try
            {
                // Load current settings
                settings = LocalSettings.LoadSettings();
            }
            catch (Exception e)
            {
                // Failed to load the settings file.  Quite possible it just doesn't exist.
                // In this case, just initialize the settings to empty
                _logger.LogTrace($"Couldn't load or locate the settings file ({e.Message}).  Initializing empty settings file");
                settings = new LocalSettings();
            }

            // Initialize line contents.
            Contents = new ReadOnlyCollection<Line>(new List<Line>
            {
                new Line($"{barPasswordElement}={GetCurrentSettingForDisplay(settings.BuildAssetRegistryPassword, "<token-from-https://maestro-prod.westus2.cloudapp.azure.com/>", true)}"),
                new Line($"{githubTokenElement}={GetCurrentSettingForDisplay(settings.GitHubToken, "<github-personal-access-token>", true)}"),
                new Line($"{azureDevOpsTokenElement}={GetCurrentSettingForDisplay(settings.AzureDevOpsToken, "<azure-devops-personal-access-token>", true)}"),
                new Line($"{barBaseUriElement}={GetCurrentSettingForDisplay(settings.BuildAssetRegistryBaseUri, "<alternate build asset registry uri if needed, otherwise leave as is>", false)}"),
                new Line(""),
                new Line("Storing the required settings...", true),
                new Line($"Set elements above depending on what you need", true),
            });
        }

        public LocalSettings settings { get; set; }

        public override bool Validate()
        {
            // No real validation required since none of the fields are mandatory
            return true;
        }
        
        /// <summary>
        /// Retrieve the string that should be displayed to the user.
        /// </summary>
        /// <param name="currentValue">Current value of the setting</param>
        /// <param name="defaultValue">Default value if the current setting value is empty</param>
        /// <param name="isSecret">If secret and current value is empty, should display ***</param>
        /// <returns>String to display</returns>
        private string GetCurrentSettingForDisplay(string currentValue, string defaultValue, bool isSecret)
        {
            if (!string.IsNullOrEmpty(currentValue))
            {
                return isSecret ? "***" : currentValue;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Parses a setting and returns the value to save.
        /// </summary>
        /// <param name="inputSetting">Input string from the file</param>
        /// <returns>
        ///     - Original setting if the setting is secret and value is still all ***
        ///     - Empty string if the setting starts+ends with <>
        ///     - New value if anything else.
        /// </returns>
        private string ParseSetting(string inputSetting, string originalSetting, bool isSecret)
        {
            string trimmedSetting = inputSetting.Trim();
            if (trimmedSetting.StartsWith('<') && trimmedSetting.EndsWith('>'))
            {
                return string.Empty;
            }
            // If the setting is secret and only contains *, then keep it the same as the input setting
            if (isSecret && trimmedSetting.Length > 0 && trimmedSetting.Replace("*", "") == string.Empty)
            {
                return originalSetting;
            }
            return trimmedSetting;
        }

        public override int ProcessContents(IList<Line> contents)
        {
            foreach (Line line in contents)
            {
                string[] keyValue = line.Text.Split("=");

                switch (keyValue[0])
                {
                    case barPasswordElement:
                        settings.BuildAssetRegistryPassword = ParseSetting(keyValue[1], settings.BuildAssetRegistryPassword, true);
                        break;
                    case githubTokenElement:
                        settings.GitHubToken = ParseSetting(keyValue[1], settings.BuildAssetRegistryPassword, true);
                        break;
                    case azureDevOpsTokenElement:
                        settings.AzureDevOpsToken = ParseSetting(keyValue[1], settings.BuildAssetRegistryPassword, true);
                        break;
                    case barBaseUriElement:
                        settings.BuildAssetRegistryBaseUri = ParseSetting(keyValue[1], settings.BuildAssetRegistryBaseUri, false);
                        break;
                    default:
                        _logger.LogWarning($"'{keyValue[0]}' is an unknown field in the authentication scope");
                        break;
                }
            }

            if (!Validate())
            {
                return Constants.ErrorCode;
            }

            return settings.SaveSettings(_logger);
        }
    }
}

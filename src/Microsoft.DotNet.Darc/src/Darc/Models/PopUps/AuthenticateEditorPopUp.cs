// Licensed to the .NET Foundation under one or more agreements.
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
                settings = LocalSettings.LoadSettingsFile();
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
                new Line("Create new BAR tokens at https://maestro-prod.westus2.cloudapp.azure.com/Account/Tokens", isComment: true),
                new Line($"{barPasswordElement}={GetCurrentSettingForDisplay(settings.BuildAssetRegistryPassword, string.Empty, true)}"),
                new Line("Create new GitHub personal access tokens at https://github.com/settings/tokens", isComment: true),
                new Line($"{githubTokenElement}={GetCurrentSettingForDisplay(settings.GitHubToken, string.Empty, true)}"),
                new Line("Create new Azure Dev Ops tokens at https://dev.azure.com/dnceng/_details/security/tokens", isComment: true),
                new Line($"{azureDevOpsTokenElement}={GetCurrentSettingForDisplay(settings.AzureDevOpsToken, string.Empty, true)}"),
                new Line($"{barBaseUriElement}={GetCurrentSettingForDisplay(settings.BuildAssetRegistryBaseUri, "<alternate build asset registry uri if needed, otherwise leave as is>", false)}"),
                new Line(""),
                new Line("Storing the required settings...", true),
                new Line($"Set elements above depending on what you need", true),
            });
        }

        public LocalSettings settings { get; set; }

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
                        settings.GitHubToken = ParseSetting(keyValue[1], settings.GitHubToken, true);
                        break;
                    case azureDevOpsTokenElement:
                        settings.AzureDevOpsToken = ParseSetting(keyValue[1], settings.AzureDevOpsToken, true);
                        break;
                    case barBaseUriElement:
                        settings.BuildAssetRegistryBaseUri = ParseSetting(keyValue[1], settings.BuildAssetRegistryBaseUri, false);
                        break;
                    default:
                        _logger.LogWarning($"'{keyValue[0]}' is an unknown field in the authentication scope");
                        break;
                }
            }

            return settings.SaveSettingsFile(_logger);
        }
    }
}

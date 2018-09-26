// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Microsoft.DotNet.Darc.Helpers
{
    /// <summary>
    /// Reads and writes the settings file
    /// </summary>
    internal class LocalSettings
    {
        public string BuildAssetRegistryPassword { get; set; }

        public string GitHubToken { get; set; }

        public string AzureDevOpsToken { get; set; }

        public string BuildAssetRegistryBaseUri { get; set; } = "https://maestro-prod.westus2.cloudapp.azure.com/";

        /// <summary>
        /// Saves the settings in the settings files
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public int SaveSettings(ILogger logger)
        {
            string settings = JsonConvert.SerializeObject(this);
            return EncodedFile.Create(Constants.SettingsFileName, settings, logger);
        }

        public static LocalSettings LoadSettings()
        {
            string settings = EncodedFile.Read(Constants.SettingsFileName);
            return JsonConvert.DeserializeObject<LocalSettings>(settings);
        }
    }
}

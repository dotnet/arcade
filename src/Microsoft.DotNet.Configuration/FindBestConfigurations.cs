// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class FindBestConfigurations : ConfigurationTask
    {
        [Required]
        public ITaskItem[] Configurations { get; set; }

        [Required]
        public string[] SupportedConfigurations { get; set; }

        public bool DoNotAllowCompatibleValues { get; set; }

        [Output]
        public ITaskItem[] BestConfigurations { get; set; }

        public override bool Execute()
        {
            LoadConfiguration();

            Dictionary<Configuration, Configuration> supportedProjectConfigurations = SupportedConfigurations.Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(c => ConfigurationFactory.ParseConfiguration(c))
                .ToDictionary(c => c, Configuration.CompatibleComparer);

            var bestConfigurations = new List<ITaskItem>();

            foreach (var configurationItem in Configurations)
            {
                var buildConfiguration = ConfigurationFactory.ParseConfiguration(configurationItem.ItemSpec);

                var compatibleConfigurations = ConfigurationFactory.GetCompatibleConfigurations(buildConfiguration, DoNotAllowCompatibleValues);

                Configuration supportedProjectConfiguration = null;

                var bestConfiguration = compatibleConfigurations.FirstOrDefault(c => supportedProjectConfigurations.TryGetValue(c, out supportedProjectConfiguration));

                if (bestConfiguration == null)
                {
                    Log.LogMessage(MessageImportance.Low, $"Could not find any applicable configuration for '{buildConfiguration}' among projectConfigurations {string.Join(", ", supportedProjectConfigurations.Select(c => c.ToString()))}");
                    Log.LogMessage(MessageImportance.Low, $"Compatible configurations: {string.Join(", ", compatibleConfigurations.Select(c => c.ToString()))}");
                }
                else
                {
                    if (supportedProjectConfiguration.IsPlaceHolderConfiguration)
                    {
                        Log.LogMessage($"Skipped configuration: {supportedProjectConfiguration.ToString()} because was marked as a placeholder configuration");
                        continue;
                    }

                    Log.LogMessage(MessageImportance.Low, $"Chose configuration {bestConfiguration}");
                    var bestConfigurationItem = new TaskItem(bestConfiguration.ToString(), (IDictionary)bestConfiguration.GetProperties());

                    // preserve metadata on the configuration that selected this
                    configurationItem.CopyMetadataTo(bestConfigurationItem);

                    // preserve the configuration that selected this
                    bestConfigurationItem.SetMetadata("BuildConfiguration", configurationItem.ItemSpec);
                    foreach(var additionalProperty in buildConfiguration.GetProperties())
                    {
                        bestConfigurationItem.SetMetadata("BuildConfiguration_" + additionalProperty.Key, additionalProperty.Value);
                    }

                    bestConfigurations.Add(bestConfigurationItem);
                }
            }

            BestConfigurations = bestConfigurations.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}


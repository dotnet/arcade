// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Configuration
{
    /// <summary>
    /// Normalize configurations by applying any insignificant properties from BuildConfigurations and expanding defaults
    /// </summary>
    public class NormalizeConfigurations : ConfigurationTask
    {
        [Required]
        public string BuildConfiguration { get; set; }

        [Required]
        public ITaskItem[] Configurations { get; set; }

        [Output]
        public ITaskItem[] NormalizedConfigurations { get; set; }

        public override bool Execute()
        {
            LoadConfiguration();

            var buildConfiguration = ConfigurationFactory.ParseConfiguration(BuildConfiguration);

            var normalizedConfigurations = new List<ITaskItem>();

            foreach(var configurationItem in Configurations.Where(c => !string.IsNullOrWhiteSpace(c.ItemSpec)))
            {
                // parse the specified configuration applying any independent properties from the BuildConfiguration.
                var configuration = ConfigurationFactory.ParseConfiguration(configurationItem.ItemSpec, baseConfiguration:buildConfiguration);

                // reuse original item as we must preserve the OriginalItemSpec metadata
                configurationItem.ItemSpec = configuration.ToFullString();
                normalizedConfigurations.Add(configurationItem);
            }

            NormalizedConfigurations = normalizedConfigurations.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}

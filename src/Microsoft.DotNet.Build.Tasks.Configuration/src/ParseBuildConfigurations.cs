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
    public class ParseBuildConfigurations : ConfigurationTask
    {
        [Required]
        public string[] BuildConfigurations { get; set; }

        [Output]
        public string TargetFrameworks { get; set; }

        public override bool Execute()
        {
            LoadConfiguration();

            List<string> targetFrameworks = new List<string>();

            foreach(string buildConfiguration in BuildConfigurations.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                // parse the specified configuration applying any independent properties from the BuildConfiguration.
                Configuration configuration = ConfigurationFactory.ParseConfiguration(buildConfiguration);

                if (!configuration.GetProperties().TryGetValue("TargetFramework", out string targetFramework))
                {
                    Log.LogError($"Could not derive a TargetFramework from BuildConfiguration '{buildConfiguration}'.");
                }

                targetFrameworks.Add(targetFramework);
            }

            TargetFrameworks = string.Join(";", targetFrameworks);

            return !Log.HasLoggedErrors;
        }
    }
}

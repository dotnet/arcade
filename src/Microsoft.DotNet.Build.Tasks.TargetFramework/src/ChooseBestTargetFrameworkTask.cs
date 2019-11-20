// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class ChooseBestTargetFrameworkTask : BuildTask
    {
        [Required]
        public string TargetFrameworkOsGroup { get; set; }
        
        [Required]
        public string[] SupportedTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public string BestTargetFramework { get; set; }

        public override bool Execute()
        {
            BestTargetFramework = new BestTfmResolver(RuntimeGraph, null).GetBestSupportedTfm(SupportedTargetFrameworks, TargetFrameworkOsGroup);
            if (BestTargetFramework == null)
            {                
                Log.LogError("Not able to find a compatible configurations");
            }
            return !Log.HasLoggedErrors;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class ChooseBestTargetFrameworksTask : BuildTask
    {
        [Required]
        public string[] TargetFrameworkOsGroupList { get; set; }

        [Required]
        public string[] SupportedTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public string[] BestTargetFrameworkArray { get; set; }

        public override bool Execute()
        {
            List<string> bestTargetFrameworkList = new List<string>();
            BestTfmResolver bestTfmResolver = new BestTfmResolver(RuntimeGraph, null);
            
            foreach (var targetFrameworkOsGroup in TargetFrameworkOsGroupList)
            {                
                string bestTargetFramework = bestTfmResolver.GetBestSupportedTfm(SupportedTargetFrameworks, targetFrameworkOsGroup);
                if (bestTargetFramework == null)
                {
                    Log.LogError("Not able to find a compatible configurations");
                }
                bestTargetFrameworkList.Add(bestTargetFramework);
            }
            BestTargetFrameworkArray = bestTargetFrameworkList.ToArray();
            return true;
        }
    }
}

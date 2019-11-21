// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class ChooseBestBinPlaceTargetFrameworks : BuildTask
    {
        [Required]
        public string[] BinPlaceTargetFrameworks { get; set; }

        [Required]
        public ITaskItem[] BuildTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] BuildTargetFrameworksWithSetBestBinPlaceTargetFramework { get; set; }

        public override bool Execute()
        {
            List<ITaskItem> BuildTargetFrameworksWithSetBestBinPlaceTargetFrameworkList = new List<ITaskItem>();
            BestTfmResolver bestTfmResolver = new BestTfmResolver(RuntimeGraph, null);

            foreach (var buildTargetFramework in BuildTargetFrameworks)
            {
                string BestTargetFramework = bestTfmResolver.GetBestSupportedTfm(BinPlaceTargetFrameworks, buildTargetFramework.ItemSpec);
                if (BestTargetFramework == null)
                {
                    Log.LogError("Not able to find a compatible configurations");
                }
                buildTargetFramework.SetMetadata("BestTargetFramework", BestTargetFramework);
                BuildTargetFrameworksWithSetBestBinPlaceTargetFrameworkList.Add(buildTargetFramework);
            }
            BuildTargetFrameworksWithSetBestBinPlaceTargetFramework = BuildTargetFrameworksWithSetBestBinPlaceTargetFrameworkList.ToArray();
            return true;
        }
    }
}

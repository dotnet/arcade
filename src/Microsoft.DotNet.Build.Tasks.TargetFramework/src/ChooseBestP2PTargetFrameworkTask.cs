// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class ChooseBestP2PTargetFrameworkTask : BuildTask
    {
        [Required]
        public string TargetFrameworkOsGroup { get; set; }

        [Required]
        public ITaskItem[] ProjectReferencesWithTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] AnnotatedProjectReferencesWithSetTargetFramework { get; set; }

        public override bool Execute()
        {      
            List<ITaskItem> AnnotatedProjectReferencesWithSetTargetFrameworkList = new List<ITaskItem>();
            BestTfmResolver bestTfmResolver = new BestTfmResolver(RuntimeGraph, null);            

            foreach (var projectReference in ProjectReferencesWithTargetFrameworks)
            {                
                string BestTargetFramework = bestTfmResolver.GetBestSupportedTfm(projectReference.GetMetadata("TargetFrameworks").Split(';'), TargetFrameworkOsGroup);
                if (BestTargetFramework == null)
                {
                    Log.LogError("Not able to find a compatible configurations");
                }
                projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + BestTargetFramework);
                AnnotatedProjectReferencesWithSetTargetFrameworkList.Add(projectReference);
            }
            AnnotatedProjectReferencesWithSetTargetFramework = AnnotatedProjectReferencesWithSetTargetFrameworkList.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}

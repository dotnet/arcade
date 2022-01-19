// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class ChooseBestTargetFrameworksTask : BuildTask
    {
        [Required]
        public string[] BuildTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Required]
        public string[] SupportedTargetFrameworks { get; set; }

        [Output]
        public string[] BestTargetFrameworks { get; set; }

        public override bool Execute()
        {
            var bestTargetFrameworkList = new HashSet<string>(BuildTargetFrameworks.Length);
            var targetframeworkResolver = new TargetFrameworkResolver(RuntimeGraph);
 
            foreach (string buildTargetFramework in BuildTargetFrameworks)
            {
                string bestTargetFramework = targetframeworkResolver.GetBestSupportedTargetFramework(SupportedTargetFrameworks, buildTargetFramework);
                if (bestTargetFramework != null)
                {
                    bestTargetFrameworkList.Add(bestTargetFramework);
                }
            }

            BestTargetFrameworks = new string[bestTargetFrameworkList.Count];
            bestTargetFrameworkList.CopyTo(BestTargetFrameworks);
 
            return !Log.HasLoggedErrors;
        }
    }
}

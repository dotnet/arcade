// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class ChooseBestTargetFrameworksTask : BuildTask
    {
        [Required]
        public ITaskItem[] SupportedTargetFrameworks { get; set; }
        
        [Required]
        public ITaskItem[] BuildTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] BestTargetFrameworks { get; set; }

        public override bool Execute()
        {
            var bestTargetFrameworkList = new List<ITaskItem>();
            var targetframeworkResolver = new TargetFrameworkResolver(RuntimeGraph);
            
            foreach (ITaskItem buildTargetFramework in BuildTargetFrameworks)
            {
                string bestTargetFramework = targetframeworkResolver.GetBestSupportedTargetFramework(SupportedTargetFrameworks.Select(t => t.ItemSpec), buildTargetFramework.ItemSpec);
                if (bestTargetFramework != null)
                {                    
                    TaskItem item = new TaskItem(SupportedTargetFrameworks.Where(t => t.ItemSpec == bestTargetFramework).First()) ;
                    buildTargetFramework.CopyMetadataTo(item);
                    bestTargetFrameworkList.Add(item);
                }
            }

            BestTargetFrameworks = bestTargetFrameworkList.ToArray();
            return !Log.HasLoggedErrors;
        }
    }
}

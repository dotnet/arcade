// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class AddTargetFrameworksToProjectTask : BuildTask
    {
        [Required]
        public string ProjectName { get; set; }
        
        [Required]
        public ITaskItem[] BestTargetFrameworks { get; set; }

        [Output]
        public ITaskItem[] InnerBuildProjects { get; set; }

        public override bool Execute()
        {
            InnerBuildProjects = new ITaskItem[BestTargetFrameworks.Length];
            for (int i = 0; i < BestTargetFrameworks.Length; i++)
            {
                InnerBuildProjects[i] = new TaskItem(ProjectName);
                InnerBuildProjects[i].SetMetadata("AdditionalProperties", "TargetFramework=" + BestTargetFrameworks[i]);
            }

            return !Log.HasLoggedErrors; ;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
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
            List<ITaskItem> innerBuildProjectsList = new List<ITaskItem>();
            foreach (var targetFramework in BestTargetFrameworks)
            {
                TaskItem item = new TaskItem(ProjectName);
                item.SetMetadata("AdditionalProperties", "TargetFramework=" + targetFramework);
                innerBuildProjectsList.Add(item);
            }

            InnerBuildProjects = innerBuildProjectsList.ToArray();
            return true;
        }
    }
}

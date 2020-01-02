// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class ChooseBestP2PTargetFrameworkTask : BuildTask
    {
        [Required]
        public string TargetFrameworkOSGroup { get; set; }

        [Required]
        public ITaskItem[] ProjectReferencesWithTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] AnnotatedProjectReferencesWithSetTargetFramework { get; set; }

        [Required]
        public bool BuildAllConfigurations { get; set; }

        public override bool Execute()
        {
            AnnotatedProjectReferencesWithSetTargetFramework = new ITaskItem[ProjectReferencesWithTargetFrameworks.Length];
            var targetFrameworkResolver = new TargetFrameworkResolver(RuntimeGraph);

            for (int i = 0; i < ProjectReferencesWithTargetFrameworks.Length; i++)
            {
                ITaskItem projectReference = ProjectReferencesWithTargetFrameworks[i];
                string[] targetFrameworks = projectReference.GetMetadata("TargetFrameworks").Split(';');
                bool refProject = projectReference.ItemSpec.Contains(@"\ref\");

                string bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, TargetFrameworkOSGroup);
                if (bestTargetFramework == null)
                {
                    if (refProject)
                    {                        
                        bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, TargetFrameworkOSGroup.Split('-')[0]);                    
                    }
                    else if (TargetFrameworkOSGroup.Contains("OSX") || TargetFrameworkOSGroup.Contains("Linux") || TargetFrameworkOSGroup.Contains("FreeBSD"))
                    {
                        // This is a temporary fix and this needs to be resolved.
                        bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, TargetFrameworkOSGroup.Split('-')[0] + "-Unix");
                    }
                }
                if (bestTargetFramework == null)
                {
                    Log.LogError($"Not able to find a compatible supported target framework for {TargetFrameworkOSGroup} in Project {Path.GetFileName(projectReference.ItemSpec)}. The Supported Configurations are {string.Join(", ", targetFrameworks)}");
                }

                if (!BuildAllConfigurations || refProject)
                {
                    projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + bestTargetFramework.Split('-')[0]);
                }
                else
                {
                    projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + bestTargetFramework);
                }
                AnnotatedProjectReferencesWithSetTargetFramework[i] = projectReference;
            }

            return !Log.HasLoggedErrors;
        }        
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class ChooseBestP2PTargetFrameworkTask : BuildTask
    {
        [Required]
        public string TargetFramework { get; set; }

        [Required]
        public ITaskItem[] ProjectReferencesWithTargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] AnnotatedProjectReferencesWithSetTargetFramework { get; set; }

        public override bool Execute()
        {
            AnnotatedProjectReferencesWithSetTargetFramework = new ITaskItem[ProjectReferencesWithTargetFrameworks.Length];
            var targetFrameworkResolver = new TargetFrameworkResolver(RuntimeGraph);

            for (int i = 0; i < ProjectReferencesWithTargetFrameworks.Length; i++)
            {
                ITaskItem projectReference = ProjectReferencesWithTargetFrameworks[i];
                string[] targetFrameworks = projectReference.GetMetadata("TargetFrameworks").Split(';');
                
                string bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, TargetFramework);
                if (bestTargetFramework == null)
                {
                    Log.LogError($"Not able to find a compatible supported target framework for {TargetFramework} in Project {Path.GetFileName(projectReference.ItemSpec)}. The Supported Configurations are {string.Join(", ", targetFrameworks)}");
                }

                projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + bestTargetFramework);                
                AnnotatedProjectReferencesWithSetTargetFramework[i] = projectReference;
            }

            return !Log.HasLoggedErrors;
        }        
    }
}

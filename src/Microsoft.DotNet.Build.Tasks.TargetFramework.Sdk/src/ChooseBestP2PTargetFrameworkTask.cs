// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class ChooseBestP2PTargetFrameworkTask : BuildTask
    {
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
                string targetFrameworksValue = projectReference.GetMetadata("RawTargetFrameworks");
                if (string.IsNullOrEmpty(targetFrameworksValue))
                {
                    targetFrameworksValue = projectReference.GetMetadata("TargetFrameworks");
                }
                string[] targetFrameworks = targetFrameworksValue.Split(';');

                string consumingTargetFramework = projectReference.GetMetadata("ConsumingTargetFramework");
                if (string.IsNullOrWhiteSpace(consumingTargetFramework))
                {
                    throw new Exception($"ConsumingTargetFramework metadata must be set for Project {Path.GetFileName(projectReference.ItemSpec)}.");
                }
                
                string bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, consumingTargetFramework);
                if (bestTargetFramework == null)
                {
                    Log.LogError($"Not able to find a compatible supported target framework for {consumingTargetFramework} in Project {Path.GetFileName(projectReference.ItemSpec)}. The Supported Configurations are {string.Join(", ", targetFrameworks)}");
                }

                projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + bestTargetFramework);                
                AnnotatedProjectReferencesWithSetTargetFramework[i] = projectReference;
            }

            return !Log.HasLoggedErrors;
        }        
    }
}

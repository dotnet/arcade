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
                string targetFrameworksValue = projectReference.GetMetadata("RawTargetFrameworks");
                if (string.IsNullOrEmpty(targetFrameworksValue))
                {
                    targetFrameworksValue = projectReference.GetMetadata("TargetFrameworks");
                }

                // Allow referencing projects with TargetFrameworks explicitely cleared out, i.e. Microsoft.Build.Traversal.
                if (!string.IsNullOrWhiteSpace(targetFrameworksValue))
                {
                    string[] targetFrameworks = targetFrameworksValue.Split(';');

                    string referringTargetFramework = projectReference.GetMetadata("ReferringTargetFramework");
                    if (string.IsNullOrWhiteSpace(referringTargetFramework))
                    {
                        referringTargetFramework = TargetFramework;
                    }

                    string bestTargetFramework = targetFrameworkResolver.GetBestSupportedTargetFramework(targetFrameworks, referringTargetFramework);
                    if (bestTargetFramework == null)
                    {
                        Log.LogError($"Not able to find a compatible supported target framework for {referringTargetFramework} in Project {Path.GetFileName(projectReference.ItemSpec)}. The Supported Configurations are {string.Join(", ", targetFrameworks)}");
                    }

                    projectReference.SetMetadata("SetTargetFramework", "TargetFramework=" + bestTargetFramework);
                    projectReference.SetMetadata("SkipGetTargetFrameworkProperties", "true");
                }

                AnnotatedProjectReferencesWithSetTargetFramework[i] = projectReference;
            }

            return !Log.HasLoggedErrors;
        }        
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.PackageValidation
{
    public class ValidatePackage : BuildTask
    {
        [Required]
        public string PackagePath { get; set; }

        [Required]
        public string ExtractFolder { get; set; }

        [Required]
        public string[] TestFrameworks { get; set; }

        public string RuntimeGraph { get; set; }

        public string NoWarn { get; set; }

        public override bool Execute()
        {
            Helpers.Initialize(TestFrameworks);

            if (!File.Exists(PackagePath))
            {
                Log.LogError($"{PackagePath} does not exist. Please check your package path.");
                return false;
            }

            Helpers.ExtractPackage(PackagePath, ExtractFolder);
            Package package = NupkgParser.CreatePackageObject(PackagePath, RuntimeGraph);
            ValidateCompileAgainstTimeRuntimeTfms(package);
            ValidateCompileAndRuntimeAgainstRelatedTfms(package);
            return !Log.HasLoggedErrors; ;
        }

        public void ValidateCompileAgainstTimeRuntimeTfms(Package package)
        {
            foreach (ContentItem compileItem in package.CompileAssets)
            {
                NuGetFramework testFramework = (NuGetFramework)compileItem.Properties["tfm"];
                ContentItem runtimeItem = package.FindBestRuntimeAssetForFramework(testFramework);

                if (runtimeItem == null)
                {
                    Log.LogError($"There is no rid less runtime asset for {testFramework.Framework}.");
                }
                else
                {
                    RunApiCompat(compileItem.Path, runtimeItem.Path);
                }
 
                foreach (var rid in package.Rids)
                {
                    runtimeItem = package.FindBestRuntimeAssetForFrameworkAndRuntime(testFramework, rid);
                    if (runtimeItem == null)
                    {
                        Log.LogError($"There is no runtime asset for {testFramework.Framework}-{rid}.");
                    }
                    else
                    {
                        RunApiCompat(compileItem.Path, runtimeItem.Path);
                    }
                }
            }
        }

        public void ValidateCompileAndRuntimeAgainstRelatedTfms(Package package)
        {
            HashSet<NuGetFramework> testFrameworks = new();
            foreach (NuGetFramework item in package.FrameworksInPackage)
            {
                if (Helpers.packageTfmMapping.ContainsKey(item))
                    testFrameworks.UnionWith(Helpers.packageTfmMapping[item]);
            }

            foreach (var framework in testFrameworks)
            {
                var compileTime = package.FindBestCompileAssetForFramework(framework);
                if (compileTime == null)
                {
                    Log.LogError($"There is no compile time asset for {framework}.");
                }

                var runtime = package.FindBestRuntimeAssetForFramework(framework);
                if (runtime == null)
                {
                    Log.LogError($"There is no runtime asset for {framework}.");
                }

                if (runtime != null && compileTime != null)
                {
                    RunApiCompat(compileTime.Path, runtime.Path);
                }

                foreach (string rid in package.Rids)
                {
                    runtime = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    // Run the api compat & version check here
                    if (runtime == null)
                    {
                        Log.LogError($"There is no runtime asset for {framework}-{rid}.");
                    }
                    else
                    {
                        RunApiCompat(compileTime.Path, runtime.Path);
                    }
                }
            }
        }

        private void RunApiCompat(string compileTimeDll, string runtTimeDll)
        {
            string compileTimeDllPath = Path.Combine(ExtractFolder, compileTimeDll);
            string runtimeDllPath = Path.Combine(ExtractFolder, runtTimeDll);
            ApiCompatRunner apiCompatRunner = new ApiCompatRunner(compileTimeDllPath, runtimeDllPath);

            foreach (var difference in apiCompatRunner.RunApiCompat(NoWarn))
            {
                Log.LogError(difference.ToString());
            }
        }
    }
}

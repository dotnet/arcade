// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;
using NuGet.ContentModel;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Diagnostics;
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


        public override bool Execute()
        {
            if (!Debugger.IsAttached) { Debugger.Launch(); } else { Debugger.Break(); };
            Helpers.Initialize(TestFrameworks);
            Helpers.ExtractPackage(PackagePath, ExtractFolder);
            Package package = NupkgParser.CreatePackageObject(PackagePath, RuntimeGraph);
            ValidateCompileAgainstTimeRuntimeTfms(package);
            ValidateCompileAndRuntimeAgainstRelatedTfms(package);
            return true;
        }

        public void ValidateCompileAgainstTimeRuntimeTfms(Package package)
        {
            foreach (var compileItem in package.CompileAssets)
            {
                NuGetFramework testFramework = (NuGetFramework)compileItem.Properties["tfm"];
                ContentItem runtimeItem = package.FindBestRuntimeAssetForFramework(testFramework);
                string compileDll = Path.Combine(ExtractFolder, compileItem.Path);
                string runtimeDll = Path.Combine(ExtractFolder, runtimeItem.Path);
            
                // Run Api Compat between compile dll and runtime dll
                IEnumerable<string> rids = package.Rids;
                RunApiCompat(compileItem.Path, runtimeItem.Path);
                foreach (var rid in rids)
                {
                    runtimeItem = package.FindBestRuntimeAssetForFrameworkAndRuntime(testFramework, rid);
                    // Run the api compat & version check here
                    RunApiCompat(compileItem.Path, runtimeItem.Path);
                }
            }
        }

        public void ValidateCompileAndRuntimeAgainstRelatedTfms(Package package)
        {
            var relatedFrameworks = package.FrameworksInPackage;
            HashSet<NuGetFramework> testFrameworks = new HashSet<NuGetFramework>();
            foreach (var item in relatedFrameworks)
            {
                if (Helpers.packageTfmMapping.ContainsKey(item))
                    testFrameworks.UnionWith(Helpers.packageTfmMapping[item]);
            }

            foreach (var framework in testFrameworks)
            {
                var compileTime = package.FindBestCompileAssetForFramework(framework);
                var runTime = package.FindBestRuntimeAssetForFramework(framework);

                // Run Api Compat between compile dll and runtime dll
                RunApiCompat(compileTime.Path, runTime.Path);
                
                IEnumerable<string> rids = package.Rids;
                foreach (var rid in rids)
                {
                    runTime = package.FindBestRuntimeAssetForFrameworkAndRuntime(framework, rid);
                    // Run the api compat & version check here
                    RunApiCompat(compileTime.Path, runTime.Path);
                }
            }
        }

        private void RunApiCompat(string compileTimeDll, string runtTimeDll)
        {
            string compileTimeDllPath = Path.Combine(ExtractFolder, compileTimeDll);
            string runTimeDllPath = Path.Combine(ExtractFolder, runtTimeDll);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageTesting
{
    public class GetCompatiblePackageTargetFrameworks : BuildTask
    {
        private static List<NuGetFramework> allTargetFrameworks = allTargetFrameworks = new();
        private static Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();

        [Required]
        public string[] PackagePaths { get; set; }

        [Output]
        public ITaskItem[] TestProjects { get; set; }

        public override bool Execute()
        {
            bool result = true;
            List<ITaskItem> testProjects = new List<ITaskItem>();
            try
            {
                Initialize();
                foreach (var packagePath in PackagePaths)
                {
                    Package package = NupkgParser.CreatePackageObject(packagePath);
                    List<NuGetFramework> packageTargetFrameworks = package.PackageAssets.Where(t => t.AssetType != AssetType.RuntimeAsset).Select(t => t.TargetFramework).Distinct().ToList();

                    List<NuGetFramework> frameworksToTest = GetTestFrameworks(packageTargetFrameworks);
                    testProjects.AddRange(CreateItemFromTestFramework(package.PackageId, package.Version, frameworksToTest, GetRidsFromPackage(package)));
                }
                
                // Removing empty items.
                TestProjects = testProjects.Where(tfm => tfm.ItemSpec != "").ToArray();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
            }
            
            return result && !Log.HasLoggedErrors;
        }

        public static List<NuGetFramework> GetTestFrameworks(List<NuGetFramework> packageTargetFrameworks)
        {
            List<NuGetFramework> frameworksToTest = new List<NuGetFramework>();

            // Testing the package installation on all tfms linked with package targetframeworks.
            foreach (var item in packageTargetFrameworks)
            {
                if (packageTfmMapping.ContainsKey(item))
                    frameworksToTest.AddRange(packageTfmMapping[item].ToList());
            }

            // Pruning the test matrix by removing the frameworks we dont want to test.
            frameworksToTest = frameworksToTest.Where(tfm => allTargetFrameworks.Contains(tfm)).ToList();

            // Adding the frameworks in the packages to the test matrix;
            frameworksToTest.AddRange(packageTargetFrameworks);
            frameworksToTest = frameworksToTest.Distinct().ToList();
            return frameworksToTest;
        }

        public static void Initialize()
        {
            // Defining the set of known frameworks that we care to test
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetCoreApp20);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetCoreApp21);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetCoreApp30);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetCoreApp31);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net50);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net45);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net451);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net452);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net46);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net461);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net462);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.Net463);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard10);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard11);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard12);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard13);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard14);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard15);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard16);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard17);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard20);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.NetStandard21);
            allTargetFrameworks.Add(FrameworkConstants.CommonFrameworks.UAP10);

            // creating a map framework in package => frameworks to test based on default compatibilty mapping.
            foreach (var item in DefaultFrameworkMappings.Instance.CompatibilityMappings)
            {
                NuGetFramework forwardTfm = item.SupportedFrameworkRange.Max;
                NuGetFramework reverseTfm = item.TargetFrameworkRange.Min;
                if (packageTfmMapping.ContainsKey(forwardTfm))
                {
                    packageTfmMapping[forwardTfm].Add(reverseTfm);
                }
                else
                {
                    packageTfmMapping.Add(forwardTfm, new HashSet<NuGetFramework> { reverseTfm });
                }
            }
        }
    
        public List<ITaskItem> CreateItemFromTestFramework(string packageId, string version, List<NuGetFramework> testFrameworks, string rids)
        {
            List<ITaskItem> testprojects = new List<ITaskItem>();
            foreach (var framework in testFrameworks)
            {
                var supportedPackage = new TaskItem(packageId);
                supportedPackage.SetMetadata("Version", version);
                supportedPackage.SetMetadata("TargetFramework", framework.ToString());
                supportedPackage.SetMetadata("TargetFrameworkShort", framework.GetShortFolderName());

                if (!String.IsNullOrEmpty(rids))
                {
                    supportedPackage.SetMetadata("RuntimeIdentifiers", rids);
                }
                testprojects.Add(supportedPackage);
            }

            return testprojects;
        }
    
        public string GetRidsFromPackage(Package package)
        {
            List<string> rids = new List<string>();
            foreach (var item in package.PackageAssets)
            {
                if (item.AssetType == AssetType.RuntimeAsset)
                {
                    string testRid = item.Rid;
                    string testArch = "-x64";
                    if (testRid == "unix")
                    {
                        if (!rids.Contains("linux" + testArch))
                            rids.Add("linux" + testArch);
                        
                        if (!rids.Contains("osx" + testArch))
                            rids.Add("osx" + testArch);
                    }
                    else
                    {
                        if (!rids.Contains(testRid + testArch))
                            rids.Add(testRid + testArch);
                    }             
                }
            }
            return string.Join(";", rids);
        }
    }    
}

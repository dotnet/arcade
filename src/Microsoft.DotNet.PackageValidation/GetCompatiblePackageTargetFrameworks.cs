// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    public class GetCompatiblePackageTargetFrameworks : BuildTask
    {
        [Required]
        public string[] PackagePaths { get; set; }

        [Required]
        public string[] TestFrameworks { get; set; }

        [Output]
        public ITaskItem[] TestPackages { get; set; }

        public override bool Execute()
        {
            bool result = true;
            List<ITaskItem> testPackages = new List<ITaskItem>();

            try
            {
                Helpers.Initialize(TestFrameworks);
                foreach (var packagePath in PackagePaths)
                {
                    Package package = NupkgParser.CreatePackageObject(packagePath, null);
                    List<NuGetFramework> frameworksToTest = GetTestFrameworks(package.FrameworksInPackage);
                    testPackages.AddRange(CreateItemFromTestFramework(package.PackageId, package.Version, frameworksToTest, package.Rids));
                }
                
                // Removing empty items.
                TestPackages = testPackages.Where(tfm => tfm.ItemSpec != "").ToArray();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
            }
            
            return result && !Log.HasLoggedErrors;
        }

        public static List<NuGetFramework> GetTestFrameworks(IEnumerable<NuGetFramework> packageTargetFrameworks)
        {
            List<NuGetFramework> frameworksToTest = new List<NuGetFramework>();

            // Testing the package installation on all tfms linked with package targetframeworks.
            foreach (var item in packageTargetFrameworks)
            {
                if (Helpers.packageTfmMapping.ContainsKey(item))
                    frameworksToTest.AddRange(Helpers.packageTfmMapping[item].ToList());
            }

            // Adding the frameworks in the packages to the test matrix;
            frameworksToTest.AddRange(packageTargetFrameworks);
            frameworksToTest = frameworksToTest.Distinct().ToList();
            return frameworksToTest;
        }
    
        public IList<ITaskItem> CreateItemFromTestFramework(string title, string version, IEnumerable<NuGetFramework> testFrameworks, IEnumerable<string> rids)
        {
            IList<ITaskItem> testprojects = new List<ITaskItem>();
            foreach (var framework in testFrameworks)
            {
                var supportedPackage = new TaskItem(title);
                supportedPackage.SetMetadata("Version", version);
                supportedPackage.SetMetadata("TargetFramework", framework.ToString());
                supportedPackage.SetMetadata("TargetFrameworkShort", framework.GetShortFolderName());

                if (rids != null)
                {
                    supportedPackage.SetMetadata("RuntimeIdentifiers", string.Join(";", rids.Select(t => t + "-x64")).Replace("unix", "linux-x64;osx"));
                }
                testprojects.Add(supportedPackage);
            }

            return testprojects;
        }
    }    
}

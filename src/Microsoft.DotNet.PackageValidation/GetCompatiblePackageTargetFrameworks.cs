// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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
            List<ITaskItem> testPackages = new List<ITaskItem>();

            try
            {
                Helpers.Initialize(TestFrameworks);
                foreach (var packagePath in PackagePaths)
                {
                    if (!File.Exists(packagePath))
                    {
                        Log.LogError($"{packagePath} does not exist. Please check the package path.");
                        continue;
                    }

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
            
            return !Log.HasLoggedErrors;
        }

        internal static List<NuGetFramework> GetTestFrameworks(IEnumerable<NuGetFramework> packageTargetFrameworks)
        {
            List<NuGetFramework> frameworksToTest = new List<NuGetFramework>();

            // Testing the package installation on all tfms linked with package targetframeworks.
            foreach (NuGetFramework item in packageTargetFrameworks)
            {
                if (Helpers.packageTfmMapping.ContainsKey(item))
                    frameworksToTest.AddRange(Helpers.packageTfmMapping[item].ToList());
            }

            // Adding the frameworks in the packages to the test matrix;
            frameworksToTest.AddRange(packageTargetFrameworks);
            frameworksToTest = frameworksToTest.Distinct().ToList();
            return frameworksToTest;
        }

        private IList<ITaskItem> CreateItemFromTestFramework(string title, string version, IEnumerable<NuGetFramework> testFrameworks, IEnumerable<string> rids)
        {
            IList<ITaskItem> generatedProjects = new List<ITaskItem>();
            foreach (NuGetFramework framework in testFrameworks)
            {
                var generatedProject = new TaskItem(title);
                generatedProject.SetMetadata("Version", version);
                generatedProject.SetMetadata("TargetFramework", framework.ToString());
                generatedProject.SetMetadata("TargetFrameworkShort", framework.GetShortFolderName());

                if (rids != null)
                {
                    generatedProject.SetMetadata("RuntimeIdentifiers", string.Join(";", rids.Select(t => t + "-x64")).Replace("unix", "linux-x64;osx"));
                }
                generatedProjects.Add(generatedProject);
            }

            return generatedProjects;
        }
    }    
}

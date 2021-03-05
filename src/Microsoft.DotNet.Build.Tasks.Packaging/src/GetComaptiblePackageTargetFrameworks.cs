// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GetCompatibilePackageTargetFrameworks : BuildTask
    {
        [Required]
        public string[] TargetFrameworks { get; set; }

        public string PackagePath { get; set; }

        [Output]
        public string[] FrameworksToTest { get; set; }

        public override bool Execute()
        {
            bool result = true;

            try
            {
                Package package = NupkgParser.CreatePackageObject(PackagePath);
                var packageTargetFrameworks = package.PackageAssets.Where(t => t.AssetType != AssetType.RuntimeAsset).Select(t => t.TargetFramework).Distinct();
                CompatibilityTable table = new CompatibilityTable(packageTargetFrameworks);
                List<string> frameworksToTest = new List<string>();

                foreach (var item in TargetFrameworks)
                {
                    IEnumerable<NuGetFramework> compatible = null;
                    table.TryGetCompatible(NuGetFramework.Parse(item), out compatible);
                    if (compatible != null)
                    {
                        frameworksToTest.Add(item);
                    }
                }

                FrameworksToTest = frameworksToTest.ToArray();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: false);
            }

            return result && !Log.HasLoggedErrors;
        }
    }
}

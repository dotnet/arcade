// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    public class Helpers
    {
        public static IList<NuGetFramework> allTargetFrameworks;
        public static Dictionary<NuGetFramework, HashSet<NuGetFramework>> packageTfmMapping = new();
        
        public static void Initialize(IEnumerable<string> nugetFrameworks)
        {
            // Defining the set of known frameworks that we care to test
            allTargetFrameworks = nugetFrameworks.Select(t => NuGetFramework.Parse(t)).ToList();

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
                    if (allTargetFrameworks.Contains(forwardTfm) && allTargetFrameworks.Contains(reverseTfm))
                        packageTfmMapping.Add(forwardTfm, new HashSet<NuGetFramework> { reverseTfm });
                }
            }
        }

        public static void ExtractPackage(string filePath, string extractFolder)
        {
            if (Directory.Exists(extractFolder))
                Directory.Delete(extractFolder, recursive: true);

            Directory.CreateDirectory(extractFolder);

            using (var stream = File.OpenRead(filePath))
            using (var zipFile = new ZipArchive(stream))
            {
                zipFile.ExtractToDirectory(extractFolder);
            }
        }
    }
}

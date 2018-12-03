// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// Replaces package dependencies with meta-package dependencies where appropriate.
    /// </summary>
    public class ApplyMetaPackages : BuildTask
    {
        /// <summary>
        /// Need the package id to ensure we don't add a meta-package reference for a package that depends on itself.
        /// </summary>
        [Required]
        public string PackageId { get; set; }
        /// <summary>
        /// Original dependencies
        /// </summary>
        [Required]
        public ITaskItem[] OriginalDependencies { get; set; }

        /// <summary>
        /// Package index files used to meta-package mappings
        /// </summary>
        public ITaskItem[] PackageIndexes { get; set; }

        /// <summary>
        /// List of package IDs which should be suppressed from remapping
        /// </summary>
        public string[] SuppressMetaPackages { get; set; }

        /// <summary>
        /// Set to true to apply the meta-package remapping
        /// </summary>
        public bool Apply { get; set; }
        
        [Output]
        public ITaskItem[] UpdatedDependencies { get; set; }
        
        public override bool Execute()
        {
            if (!Apply)
            {
                UpdatedDependencies = OriginalDependencies;
                return true;
            }

            var index = PackageIndex.Load(PackageIndexes.Select(pi => pi.GetMetadata("FullPath")));
            List<ITaskItem> updatedDependencies = new List<ITaskItem>();

            var suppressMetaPackages = new HashSet<string>(SuppressMetaPackages.NullAsEmpty(), StringComparer.OrdinalIgnoreCase);

            // We cannot add a dependency to a meta-package from a package that itself is part of the meta-package otherwise we create a cycle
            var metaPackageThisPackageIsIn = index.MetaPackages.GetMetaPackageId(PackageId);
            if (metaPackageThisPackageIsIn != null)
            {
                suppressMetaPackages.Add(metaPackageThisPackageIsIn);
            }

            // keep track of meta-package dependencies to add by framework so that we only add them once per framework.
            Dictionary<string, HashSet<NuGetFramework>> metaPackagesToAdd = new Dictionary<string, HashSet<NuGetFramework>>();

            foreach (var originalDependency in OriginalDependencies)
            {
                var metaPackage = index.MetaPackages.GetMetaPackageId(originalDependency.ItemSpec);

                if (metaPackage != null && !suppressMetaPackages.Contains(metaPackage))
                {
                    // convert to meta-package dependency
                    var tfm = originalDependency.GetMetadata("TargetFramework");
                    var fx = NuGetFramework.Parse(tfm);

                    HashSet<NuGetFramework> metaPackageFrameworks;

                    if (!metaPackagesToAdd.TryGetValue(metaPackage, out metaPackageFrameworks))
                    {
                        metaPackagesToAdd[metaPackage] = metaPackageFrameworks = new HashSet<NuGetFramework>();
                    }

                    metaPackageFrameworks.Add(fx);
                }
                else
                {
                    updatedDependencies.Add(originalDependency);
                }
            }

            updatedDependencies.AddRange(metaPackagesToAdd.SelectMany(pair => pair.Value.Select(tfm => CreateDependency(pair.Key, tfm))));

            UpdatedDependencies = updatedDependencies.ToArray();

            return !Log.HasLoggedErrors;
        }

        private ITaskItem CreateDependency(string id, NuGetFramework targetFramework)
        {
            var item = new TaskItem(id);
            item.SetMetadata("TargetFramework", targetFramework.GetShortFolderName());
            return item;
        }
        
    }
}

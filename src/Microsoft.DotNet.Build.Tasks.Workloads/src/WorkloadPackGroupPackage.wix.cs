// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    internal class WorkloadPackGroupPackage
    {
        public WorkloadManifestPackage ManifestPackage { get; }

        public List<WorkloadPackPackage> Packs { get; set; } = new();

        public string Platform { get; }

        public string WorkloadName { get; }

        public string Id => WorkloadName + ".WorkloadPacks";

        public WorkloadPackGroupPackage(string platform, WorkloadManifestPackage manifestPackage, string workloadName)
        {
            Platform = platform;
            ManifestPackage = manifestPackage;
            WorkloadName = workloadName;
        }

        public MsiMetadata GetMsiMetadata()
        {
            return new MsiMetadata(Id, ManifestPackage.PackageVersion, ManifestPackage.MsiVersion, ManifestPackage.Authors,
                title: WorkloadName + " packs", ManifestPackage.LicenseUrl, swixPackageId: Id);
        }
    }
}

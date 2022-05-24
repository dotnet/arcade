// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    internal class MsiMetadata
    {
        public string Id
        {
            get;
        }

        public NuGetVersion PackageVersion
        {
            get;
        }

        public Version MsiVersion
        {
            get;
        }

        public string Authors
        {
            get;
        }

        public string Title
        {
            get;
        }

        public string LicenseUrl
        {
            get;
        }

        public string SwixPackageId
        {
            get;
        }

        public MsiMetadata(string id, NuGetVersion packageVersion, Version msiVersion, string authors, string title, string licenseUrl, string swixPackageId)
        {
            Id = id;
            PackageVersion = packageVersion;
            MsiVersion = msiVersion;
            Authors = authors;
            Title = title;
            LicenseUrl = licenseUrl;
            SwixPackageId = swixPackageId;
        }

        public static MsiMetadata Create(WorkloadPackageBase package)
        {
            return new(
                package.Id,
                package.PackageVersion,
                package.MsiVersion,
                package.Authors,
                package.Title,
                package.LicenseUrl,
                package.SwixPackageId
                );
        }
    }
}

#nullable disable

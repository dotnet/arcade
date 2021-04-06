// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Represents an MSI package.
    /// </summary>
    public class MsiPackage
    {
        public string InstallDir
        {
            get;
        }

        public string OutputFile
        {
            get;
        }

        /// <summary>
        /// The target platform of the MSI.
        /// </summary>
        public string Platform
        {
            get;
        }

        public string SourcePackage
        {
            get;
        }

        public MsiPackage(string sourcePackage, string platform, string installDir)
        {
            SourcePackage = sourcePackage;
            Platform = platform;
            InstallDir = installDir;
        }
       
        public static IEnumerable<MsiPackage> Create(string sourcePackage, string installDir, params string[] platforms)
        {
            foreach (string platform in platforms)
            {
                yield return new MsiPackage(sourcePackage, installDir, platform);
            }
        }

       
    }
}

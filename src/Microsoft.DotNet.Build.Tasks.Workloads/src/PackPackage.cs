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
    public class PackPackage
    {
        private List<MsiPackage> _msis;

        public string Id
        {
            get;
        }

        public string InstallDir
        {
            get;
        }

        public IEnumerable<MsiPackage> Msis
        {
            get
            {
                if (_msis == null)
                {
                    _msis = GetMsiPackages();
                }

                return _msis;
            }
        }

        public string PackagesPath
        {
            get;
        }

        public string SourcePackage
        {
            get;
            set;
        }

        private WorkloadPack Pack
        {
            get;
        }

        public PackPackage(WorkloadPack pack, string packagesPath, NuGetVersion version)
        {
            Pack = pack;
            Id = Pack.Id.ToString();
            InstallDir = GetInstallDir(pack.Kind);
            PackagesPath = packagesPath;
        }

        public PackPackage(string sourcePackage, string installDir, params string[] platforms)
        {
            SourcePackage = sourcePackage;
            InstallDir = installDir;
        }

        private List<MsiPackage> GetMsiPackages()
        {
            List<MsiPackage> msis = new();

            //if (!string.IsNullOrEmpty())

            string sourcePackage = Path.Combine(PackagesPath, $"{Pack.Id}.{Pack.Version}.nupkg");

            if (Pack.IsAlias)
            {
                foreach (string rid in Pack.AliasTo.Keys)
                {
                    sourcePackage = Path.Combine(PackagesPath, $"{Pack.AliasTo[rid]}.{Pack.Version}.nupkg");

                    switch (rid)
                    {
                        case "win":
                        case "win7":
                        case "win10":
                        case "any":
                            msis.AddRange(MsiPackage.Create(sourcePackage, InstallDir, "x86", "x64", "arm64"));
                            break;
                        case "win-x86":
                        case "win7-x86":
                        case "win10-x86":
                            msis.Add(new(sourcePackage, "x86", InstallDir));
                            break;
                        case "win-x64":
                        case "win7-x64":
                        case "win10-x64":
                            msis.Add(new(sourcePackage, "x64", InstallDir));
                            break;
                        case "win-arm64":
                        case "win7-arm64":
                        case "win10-arm64":
                            msis.Add(new(sourcePackage, "x64", InstallDir));
                            break;
                        default:
                            // We don't care about non-Windows packages.
                            continue;
                    }
                }
            }
            else
            {
                msis.AddRange(MsiPackage.Create(sourcePackage, InstallDir, "x86", "x64", "arm64"));
            }

            return msis;
        }

        /// <summary>
        /// Gets the installation directory based on the kind of workload pack.
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        private static string GetInstallDir(WorkloadPackKind kind)
        {
            switch (kind)
            {
                case WorkloadPackKind.Framework:
                case WorkloadPackKind.Sdk:
                    return "packs";
                case WorkloadPackKind.Library:
                    return "library-packs";
                case WorkloadPackKind.Template:
                    return "templates";
                case WorkloadPackKind.Tool:
                    return "tool-packs";
                default:
                    throw new ArgumentException($"Unknown package kind: {kind}");
            }
        }
    }
}

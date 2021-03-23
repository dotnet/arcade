// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads.src
{
    public class VisualStudioWorkload
    {
        private static readonly string[] MsiPlatforms = new string[] { "x86", "x64", "arm64" };

        private List<WorkloadPackMsiData> _msiPacks;

        public string ManifestFile
        {
            get;
        }

        public string OutputPath
        {
            get;
        }

        public string PackagesPath
        {
            get;
        }

        public IEnumerable<WorkloadPackMsiData> MsiPacks
        {
            get
            {
                if (_msiPacks == null)
                {
                    _msiPacks = GetPackData();
                }

                return _msiPacks;
            }
        }

        private TaskLoggingHelper Log
        {
            get;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="workloadManifest"></param>
        public VisualStudioWorkload(string manifestFile, string outputPath, string packagesPath, TaskLoggingHelper log)
        {
            ManifestFile = manifestFile;
            OutputPath = outputPath;
            PackagesPath = packagesPath;
            Log = log;
        }

        private List<WorkloadPackMsiData> GetPackData()
        {
            Log?.LogMessage(MessageImportance.Low, $"GetPackData: processing {ManifestFile}");
            WorkloadManifest manifest = WorkloadManifestReader.ReadWorkloadManifest(File.OpenRead(ManifestFile));
            List<WorkloadPackMsiData> msiPacks = new();

            foreach (WorkloadPackId packId in manifest.Packs.Keys)
            {
                WorkloadPack pack = manifest.Packs[packId];

                if (!pack.IsAlias)
                {
                    // Need to generate x86, x64 and arm64 MSI
                    string sourcePackage = Path.Combine(PackagesPath, $"{pack.Id}.{pack.Version}.nupkg");

                    if (!File.Exists(sourcePackage))
                    {
                        throw new FileNotFoundException($"Cannot find source package: {sourcePackage}");
                    }

                    foreach (string platform in MsiPlatforms)
                    {
                        Log?.LogMessage(MessageImportance.Low, $"GetPackData: source: {sourcePackage}, platform: {platform}");
                        msiPacks.Add(new WorkloadPackMsiData(sourcePackage, GetInstallDir(pack.Kind), platform));
                        
                    }
                }
                else
                {
                    // Only generate MSIs for the provided RIDs

                }
            }

            return msiPacks;
        }

        private string GetInstallDir(WorkloadPackKind kind)
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

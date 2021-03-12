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
    public class GenerateVisualStudioWorkload : Task
    {
        /// <summary>
        /// The NuGet package containing the workload manifest and targets.
        /// </summary>
        [Required]
        public string WorkloadManifestPackage
        {
            get;
            set;
        }

        /// <summary>
        /// The base intermediate output path. 
        /// </summary>
        /// [Required]
        public string IntermediateBaseOutputPath
        {
            get;
            set;
        }

        public string VisualStudioComponentSourcePath;

        public string PackageFeed
        {
            get;
            set;
        }

        public string PackageSourcePath
        {
            get;
            set;
        }

        public string SrcSetupPackagesPath => Path.Combine(IntermediateBaseOutputPath, "src", "setuppackages");

        public string WorkloadPackPackages => Path.Combine(IntermediateBaseOutputPath, "packs");

        public string WorkloadDefinitionPath;

        /// <summary>
        /// The path where the workload manifest.json file resides.
        /// </summary>
        public string WorkloadManifestPath => "";

        public override bool Execute()
        {
            try
            {
                PackageSourcePath = Path.GetFullPath(PackageSourcePath);
                ProcessWorkloadManifest();
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);                
            }

            return !Log.HasLoggedErrors;
        }

        public void ProcessWorkloadManifest()
        {
            NugetPackage workloadPackage = new NugetPackage(WorkloadManifestPackage, Log);
            WorkloadDefinitionPath = Path.Combine(IntermediateBaseOutputPath, workloadPackage.Id, workloadPackage.Version.ToString());            
            workloadPackage.Extract(WorkloadDefinitionPath, Enumerable.Empty<string>());

            string manifestJsonPath = Directory.GetFiles(WorkloadDefinitionPath, "workloadManifest.json").FirstOrDefault();

            if (manifestJsonPath is null)
            {
                throw new FileNotFoundException($"Unable to locate a workload manifest file under '{WorkloadDefinitionPath}'.");
            }

            WorkloadManifest manifest = WorkloadManifestReader.ReadWorkloadManifest(File.OpenRead(manifestJsonPath));

            foreach (WorkloadDefinitionId id in manifest.Workloads.Keys)
            {
                string safeComponentId = Utils.ToSafeId(id.ToString());
                Log.LogMessage(MessageImportance.High, $"Processing workload definition: {id} ({safeComponentId})");

                VisualStudioComponentSourcePath = Path.Combine(IntermediateBaseOutputPath, safeComponentId,
                  workloadPackage.Version.ToString());

                EmbeddedTemplates.Extract("component.swr", VisualStudioComponentSourcePath);
                EmbeddedTemplates.Extract("component.res.swr", VisualStudioComponentSourcePath);
                EmbeddedTemplates.Extract("component.swixproj", VisualStudioComponentSourcePath, safeComponentId + ".swixproj");

                foreach (WorkloadPackId packId in manifest.Packs.Keys)
                {
                    GeneratePacks(manifest.Packs[packId]);
                }
            }
            //object manifestJson = JsonSerializer.Deserialize(manifestJsonPath);
        }

        public void GeneratePacks(WorkloadPack pack)
        {
            Log.LogMessage(MessageImportance.High, $"Generating MSI from workload pack: {pack.Id}");

            if (!pack.IsAlias)
            {
                string sourcePackage = Path.Combine(PackageSourcePath, $"{pack.Id}.{pack.Version}.nupkg");

                if (!File.Exists(sourcePackage))
                {
                    throw new FileNotFoundException($"Unable to find workload pack '{sourcePackage}'");
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.High, $"Processing aliases");
            }    


            //NuGet.Common.ILogger logger = NullLogger.Instance;
            //CancellationToken cancellationToken = CancellationToken.None;

            //SourceCacheContext cache = new SourceCacheContext();
            //SourceRepository repository = Repository.Factory.GetCoreV3(PackageFeed);
            //FindPackageByIdResource resource = repository.GetResourceAsync<FindPackageByIdResource>().Result;

            
            //NuGetVersion packageVersion = new NuGetVersion("12.0.1");
            //using MemoryStream packageStream = new MemoryStream();

            //bool x = resource.CopyNupkgToStreamAsync(
            //    pack.Id.ToString(),
            //    new NuGetVersion(pack.Version),
            //    packageStream,
            //    cache,
            //    logger,
            //    cancellationToken).Result;            
        }

        private Dictionary<string, string> GetReplacementTokens()
        {
            return new Dictionary<string, string>()
            {
                //{"__VS_PACKAGE_NAME__", ComponentName },
                //{"__VS_PACKAGE_VERSION__", Version },
                //{"__VS_IS_UI_GROUP__", IsUiGroup ? "yes" : "no" },
                //{"__VS_COMPONENT_TITLE__", ComponentTitle },
                //{"__VS_COMPONENT_DESCRIPTION__", ComponentDescription },
                //{"__VS_COMPONENT_CATEGORY__", ComponentCategory }
            };
        }
    }
}

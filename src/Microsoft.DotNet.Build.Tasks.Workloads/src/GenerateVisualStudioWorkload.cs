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
    public class GenerateVisualStudioWorkload : GenerateTaskBase
    {
        /// <summary>
        /// The NuGet package containing the workload manifest and targets.
        /// </summary>

        public string WorkloadManifestPackage
        {
            get;
            set;
        }

        /// <summary>
        /// The manifest file describing the workload.
        /// </summary>
        public string WorkloadManifest
        {
            get;
            set;
        }

        /// <summary>
        /// A set of workload manifest items.
        /// </summary>
        public ITaskItem[] WorkloadManifests
        {
            get;
            set;
        }

        public string OutputPath
        {
            get;
            set;
        }

        public string VisualStudioComponentSourcePath;

        public string PackagesPath
        {
            get;
            set;
        }

        //public string WixToolsetPath
        //{
        //    get;
        //    set;
        //}

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
                PackagesPath = Path.GetFullPath(PackagesPath);
                ProcessWorkloadManifests();
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        //public IEnumerable<> GetWorkloadPacks()
        //{

        //}

        public void ProcessWorkloadManifests()
        {
            List<WorkloadPackMsiData> msiPacks = new();

            foreach (ITaskItem workloadManifestItem in WorkloadManifests)
            {
                var VSW = new VisualStudioWorkload(workloadManifestItem.ItemSpec, OutputPath, PackagesPath, Log);

                msiPacks.AddRange(VSW.MsiPacks);

               
            }

            // Dedupe to account for potentially shared packs across all the workloads
            //IEnumerable<GenerateWorkloadPackMsi> generateMsiTasks = msiPacks.
            //    Distinct().
            //    Select(p => new GenerateWorkloadPackMsi(BuildEngine, p, IntermediateBaseOutputPath, WixToolsetPath, OutputPath));

            //foreach (GenerateWorkloadPackMsi task in generateMsiTasks)
            //{
            //    if (!task.Execute())
            //    {
            //        throw new Exception($"Failed to generate workload pack for {task.SourcePackage} ({task.Platform}).");
            //    }
            //}


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
                string sourcePackage = Path.Combine(PackagesPath, $"{pack.Id}.{pack.Version}.nupkg");

                if (!File.Exists(sourcePackage))
                {
                    throw new FileNotFoundException($"Unable to find workload pack '{sourcePackage}'");
                }

                //GenerateInstaller giTask = new GenerateInstaller(BuildEngine)
                //{
                //    IntermediateBaseOutputPath = this.IntermediateBaseOutputPath,
                //    SourcePackage = sourcePackage,
                //    WixToolsetPath = this.WixToolsetPath,

                //};
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

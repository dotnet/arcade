using Microsoft.Build.Framework;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class ChooseBestTargetFrameworkReference : BuildTask
    {
        [Required]
        public string TargetFrameworkOsGroup { get; set; }

        [Required]
        public ITaskItem[] TargetFrameworks { get; set; }

        [Required]
        public string RuntimeGraph { get; set; }

        [Output]
        public ITaskItem[] TargetFrameworksWithTargetFramework { get; set; }

        public override bool Execute()
        {
            List<ITaskItem> tfmList = new List<ITaskItem>();
            if (!Debugger.IsAttached) { Debugger.Launch(); } else { Debugger.Break(); }
            foreach (var tfm_os in TargetFrameworks)
            {
                var contentCollection = new ContentItemCollection();
                contentCollection.Load(tfm_os.GetMetadata("TargetFrameworks").Split(';').Select(t => t + '/')) ;

                var runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeGraph);
                var conventions = new ManagedCodeConventions(runtimeGraph);

                var configStringPattern = new PatternSet(
                        conventions.Properties,
                        groupPatterns: new PatternDefinition[]
                        {
                        new PatternDefinition("{tfm}/", table: DotnetAnyTable),
                        new PatternDefinition("{tfm}-{rid}/", table: DotnetAnyTable)
                        },
                        pathPatterns: new PatternDefinition[]
                        {
                        new PatternDefinition("{tfm}/", table: DotnetAnyTable),
                        new PatternDefinition("{tfm}-{rid}/", table: DotnetAnyTable)
                        });

                string tfm = TargetFrameworkOsGroup;
                string osGroup = string.Empty;
                if (TargetFrameworkOsGroup.Contains("-"))
                {
                    string[] splitStrings = TargetFrameworkOsGroup.Split('-');
                    tfm = splitStrings[0];
                    osGroup = splitStrings[1];
                }

                var criteria = conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(tfm), osGroup);
                string BestTargetFramework = contentCollection.FindBestItemGroup(criteria, configStringPattern)?.Items[0].Path;
                if (BestTargetFramework == null)
                {
                    Log.LogError("Not able to find a compatible configurations");
                }
                else
                {
                    BestTargetFramework = BestTargetFramework.Remove(BestTargetFramework.Length - 1);
                }
                tfm_os.SetMetadata("SetTargetFramework", "TargetFramework=" + BestTargetFramework);
                tfmList.Add(tfm_os);
            }
            TargetFrameworksWithTargetFramework = tfmList.ToArray();
            return !Log.HasLoggedErrors;
        }

        private static readonly PatternTable DotnetAnyTable = new PatternTable(new[]
        {
            new PatternTableEntry(
                "tfm",
                "any",
                FrameworkConstants.CommonFrameworks.DotNet)
        });
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class CreateXUnitWorkItems : Build.Utilities.Task
    {
        [Required]
        public string[] XUnitDllDirectories { get; set; }

        [Required]
        public string[] XUnitDllPaths { get; set; }

        private Dictionary<string, string> DirectoriesToPathMap;

        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        public override bool Execute()
        {
            if (XUnitDllDirectories.Length < 1)
            {
                Log.LogError("No XUnit Projects found");
            }
            else if (XUnitDllDirectories.Length > XUnitDllPaths.Length)
            {
                Log.LogError("Not all XUnit projects produced assemblies.");
            }
            else if (XUnitDllDirectories.Length < XUnitDllPaths.Length)
            {
                Log.LogError("More XUnit assemblies were found than projects which is alarming.");
            }

            DirectoriesToPathMap = new Dictionary<string, string>();
            try
            {
                for (int i = 0; i < XUnitDllDirectories.Length; i++)
                {
                    DirectoriesToPathMap.Add(XUnitDllDirectories[i], XUnitDllPaths[i]);
                }
            }
            catch (ArgumentException e)
            {
                Log.LogError("Two identical publish paths were provided", e.StackTrace);
            }

            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            XUnitWorkItems = await Task.WhenAll(XUnitDllDirectories.Select(PrepareWorkItem));

            return;
        }

        private async Task<ITaskItem> PrepareWorkItem(string publishPath)
        {
            await Task.Yield();

            string assemblyName = Path.GetFileNameWithoutExtension(DirectoriesToPathMap[publishPath]);
            string command = $"dotnet xunit.console.dll {assemblyName}.dll -xml testResults.xml";

            Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishPath}, Command: {command}");

            ITaskItem workItem = new WiTaskItem(assemblyName, publishPath, command);
            return workItem;
        }

        private class WiTaskItem : ITaskItem
        {
            public WiTaskItem()
            {
                Metadata = new Dictionary<string, string>();
            }
            public WiTaskItem(string identity, string payloadDirectory, string command)
            {
                Metadata = new Dictionary<string, string>();
                ItemSpec = identity;
                SetMetadata("Identity", identity);
                SetMetadata("PayloadDirectory", payloadDirectory);
                SetMetadata("Command", command);
            }

            public string ItemSpec { get; set; }

            private Dictionary<string, string> Metadata;

            public ICollection MetadataNames { get { return Metadata.Keys; } }

            public int MetadataCount { get { return Metadata.Count; } }

            public IDictionary CloneCustomMetadata()
            {
                return Metadata;
            }

            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                foreach (string key in Metadata.Keys)
                {
                    destinationItem.SetMetadata(key, Metadata[key]);
                }
            }

            public string GetMetadata(string metadataName)
            {
                return (Metadata.ContainsKey(metadataName) ? Metadata[metadataName] : "");
            }

            public void RemoveMetadata(string metadataName)
            {
                if (Metadata.ContainsKey(metadataName))
                {
                    Metadata.Remove(metadataName);
                }
            }

            public void SetMetadata(string metadataName, string metadataValue)
            {
                if (Metadata.ContainsKey(metadataName))
                {
                    Metadata[metadataName] = metadataValue;
                }
                else
                {
                    Metadata.Add(metadataName, metadataValue);
                }
            }
        }
    }
}

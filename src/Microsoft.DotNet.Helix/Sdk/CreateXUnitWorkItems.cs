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
        /// <summary>
        /// An array of XUnit project publish directories
        /// </summary>
        [Required]
        public string[] XUnitDllDirectories { get; set; }

        /// <summary>
        /// An array of paths to the DLLs created by an XUnit project
        /// </summary>
        [Required]
        public string[] XUnitDllPaths { get; set; }

        private Dictionary<string, string> _directoriesToPathMap;

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        public override bool Execute()
        {
            if (XUnitDllDirectories is null || XUnitDllPaths is null)
            {
                Log.LogError($"Required metadata {(((XUnitDllDirectories is null) != (XUnitDllPaths is null)) ? (XUnitDllDirectories is null ? "XUnitDllDirectories is " : "XUnitDllPaths is ") : "XUnitDllDirectories and XUnitDllPaths are ")} null");
                return false;
            }

            if (XUnitDllDirectories.Length < 1)
            {
                Log.LogError("No XUnit Projects found");
            }
            else if (XUnitDllDirectories.Length > XUnitDllPaths.Length)
            {
                Log.LogError("Not all XUnit projects produced assemblies");
            }
            else if (XUnitDllDirectories.Length < XUnitDllPaths.Length)
            {
                Log.LogError("More XUnit assemblies were found than projects");
            }

            _directoriesToPathMap = new Dictionary<string, string>();
            for (int i = 0; i < XUnitDllDirectories.Length; i++)
            {
                if (_directoriesToPathMap.ContainsKey(XUnitDllDirectories[i]))
                {
                    Log.LogError("Two identical publish paths were provided");
                }
                else
                {
                    _directoriesToPathMap.Add(XUnitDllDirectories[i], XUnitDllPaths[i]);
                }
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

            string assemblyName = Path.GetFileNameWithoutExtension(_directoriesToPathMap[publishPath]);
            string command = $"dotnet xunit.console.dll {assemblyName}.dll -xml testResults.xml";

            Log.LogMessage($"Creating work item with properties Identity: {assemblyName}, PayloadDirectory: {publishPath}, Command: {command}");

            ITaskItem workItem = new WorkItemTaskItem(assemblyName, publishPath, command);
            return workItem;
        }

        private class WorkItemTaskItem : ITaskItem
        {
            public WorkItemTaskItem()
            {
                _metadata = new Dictionary<string, string>();
            }
            public WorkItemTaskItem(string identity, string payloadDirectory, string command) : this()
            {
                ItemSpec = identity ?? "";
                SetMetadata("Identity", identity ?? "");
                SetMetadata("PayloadDirectory", payloadDirectory ?? "");
                SetMetadata("Command", command ?? "");
            }

            public string ItemSpec { get; set; }

            private Dictionary<string, string> _metadata;

            public ICollection MetadataNames { get { return _metadata.Keys; } }

            public int MetadataCount { get { return _metadata.Count; } }

            public IDictionary CloneCustomMetadata()
            {
                return _metadata;
            }

            public void CopyMetadataTo(ITaskItem destinationItem)
            {
                foreach (string key in _metadata.Keys)
                {
                    destinationItem.SetMetadata(key, _metadata[key]);
                }
            }

            public string GetMetadata(string metadataName)
            {
                return (_metadata.ContainsKey(metadataName) ? _metadata[metadataName] : "");
            }

            public void RemoveMetadata(string metadataName)
            {
                if (_metadata.ContainsKey(metadataName))
                {
                    _metadata.Remove(metadataName);
                }
            }

            public void SetMetadata(string metadataName, string metadataValue)
            {
                if (_metadata.ContainsKey(metadataName))
                {
                    _metadata[metadataName] = metadataValue;
                }
                else
                {
                    _metadata.Add(metadataName, metadataValue);
                }
            }
        }
    }
}

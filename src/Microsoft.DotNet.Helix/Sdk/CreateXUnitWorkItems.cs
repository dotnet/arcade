using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Sdk
{
    class CreateXUnitWorkItems : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string[] XUnitDlls { get; set; }

        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            XUnitWorkItems = await Task.WhenAll(XUnitDlls.Select(PrepareWorkItem));

            return;
        }

        private async Task<ITaskItem> PrepareWorkItem(string project)
        {
            await Task.Yield();

            string projectName = Path.GetFileNameWithoutExtension(project);
            string command = $"dotnet xunit.console.dll {projectName}.dll -xml testResults.xml";

            ITaskItem workItem = new WiTaskItem(projectName, project, command);
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
                throw new NotImplementedException();
            }

            public string GetMetadata(string metadataName)
            {
                return Metadata[metadataName];
            }

            public void RemoveMetadata(string metadataName)
            {
                Metadata.Remove(metadataName);
            }

            public void SetMetadata(string metadataName, string metadataValue)
            {
                Metadata[metadataName] = metadataValue;
            }
        }
    }
}

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
        public string[] XUnitDlls { get; set; }

        [Output]
        public ITaskItem[] XUnitWorkItems { get; set; }

        public override bool Execute()
        {
            if (XUnitDlls.Length < 1)
            {
                Log.LogError("No XUnit Projects found");
            }
            Console.WriteLine("Execute 1");
            ExecuteAsync().GetAwaiter().GetResult();
            Console.WriteLine("Execute 2");
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            Console.WriteLine("ExecuteAsync 1");

            XUnitWorkItems = await Task.WhenAll(XUnitDlls.Select(PrepareWorkItem));

            Console.WriteLine("ExecuteAsync 2");

            return;
        }

        private async Task<ITaskItem> PrepareWorkItem(string project)
        {
            await Task.Yield();

            string projectName = Path.GetFileNameWithoutExtension(project);
            string command = $"dotnet xunit.console.dll {projectName}.dll -xml testResults.xml";

            Console.WriteLine($"Hi! Identity: {projectName}, PayloadDirectory: {project}, Command: {command}");

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

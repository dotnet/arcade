using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class SendHelixJob : HelixTask
    {
        [Required]
        public string Source { get; set; }

        [Required]
        public string Type { get; set; }

        [Required]
        public string Build { get; set; }

        [Required]
        public string TargetQueue { get; set; }

        [Output]
        public string JobCorrelationId { get; set; }

        /// <summary>
        /// A set of directories that will be zipped up and sent as Correlation Payloads for the helix job.
        /// </summary>
        public ITaskItem[] CorrelationPayloads { get; set; }

        /// <summary>
        /// A set of work items that will run in the helix job.
        /// </summary>
        public ITaskItem[] WorkItems { get; set; }

        protected override async Task ExecuteCore()
        {

            var def = HelixApi.Job.Define()
                .WithSource(Source)
                .WithType(Type)
                .WithBuild(Build)
                .WithTargetQueue(TargetQueue);

            if (CorrelationPayloads != null)
            {
                foreach (var correlationPayload in CorrelationPayloads)
                {
                    def = AddCorrelationPayload(def, correlationPayload);
                }
            }

            if (WorkItems != null)
            {
                foreach (var workItem in WorkItems)
                {
                    def = AddWorkItem(def, workItem);
                }
            }
            else
            {
                Log.LogError("SendHelixJob given no WorkItems to send.");
            }


            // don't send the job if we have errors
            if (Log.HasLoggedErrors)
            {
                return;
            }

            var job = await def.SendAsync();
            JobCorrelationId = job.CorrelationId;
        }

        private IJobDefinition AddWorkItem(IJobDefinition def, ITaskItem workItem)
        {
            if (!GetRequiredMetadata(workItem, "Identity", out string name))
            {
                return def;
            }

            if (!GetRequiredMetadata(workItem, "Command", out string command))
            {
                return def;
            }

            var wiWithPayload = def.DefineWorkItem(name)
                .WithCommand(command);


            var payload = workItem.GetMetadata("PayloadDirectory");
            IWorkItemDefinition wi;
            if (!string.IsNullOrEmpty(payload))
            {
                wi = wiWithPayload.WithDirectoryPayload(payload);
            }
            else
            {
                wi = wiWithPayload.WithEmptyPayload();
            }

            var timeoutString = workItem.GetMetadata("Timeout");
            if (!string.IsNullOrEmpty(timeoutString))
            {
                if (TimeSpan.TryParse(timeoutString, out TimeSpan timeout))
                {
                    wi = wi.WithTimeout(timeout);
                }
                else
                {
                    Log.LogWarning($"Timeout value '{timeoutString}' could not be parsed.");
                }
            }

            return wi.AttachToJob();
        }

        private bool GetRequiredMetadata(ITaskItem item, string key, out string value)
        {
            value = item.GetMetadata(key);
            if (string.IsNullOrEmpty(value))
            {
                Log.LogError($"Item '{item.ItemSpec}' missing required metadata '{key}'.");
                return false;
            }

            return true;
        }

        private IJobDefinition AddCorrelationPayload(IJobDefinition def, ITaskItem correlationPayload)
        {
            var path = correlationPayload.GetMetadata("FullPath");

            if (!Directory.Exists(path))
            {
                Log.LogError($"Correlation Payload Directory '{path}' not found.");
                return def;
            }

            return def.WithCorrelationPayloadDirectory(path);
        }
    }
}

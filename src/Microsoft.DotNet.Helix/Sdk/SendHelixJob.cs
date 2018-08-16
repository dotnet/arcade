using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class SendHelixJob : HelixTask
    {
        /// <summary>
        ///   The 'source' value reported to helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Source { get; set; }

        /// <summary>
        ///   The 'type' value reported to helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Type { get; set; }

        /// <summary>
        ///   The 'build' value reported to helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Build { get; set; }

        /// <summary>
        ///   The helix queue this job should run on
        /// </summary>
        [Required]
        public string TargetQueue { get; set; }

        /// <summary>
        ///   When the task finishes, the correlation id of the job that has been created
        /// </summary>
        [Output]
        public string JobCorrelationId { get; set; }

        /// <summary>
        ///   A set of directories that will be zipped up and sent as Correlation Payloads for the helix job.
        /// </summary>
        /// <remarks>
        ///   Metadata Used:
        ///     FullPath - This path is required to be a directory and will be zipped up and used as a correlation payload
        /// </remarks>
        public ITaskItem[] CorrelationPayloads { get; set; }

        /// <summary>
        ///   A set of work items that will run in the helix job.
        /// </summary>
        /// <remarks>
        ///   Required Metadata:
        ///     Identity - The WorkItemName
        ///     Command - The command that is invoked to execute the work item
        ///   Optional Metadata:
        ///     PayloadDirectory - A directory that will be zipped up and sent as the Work Item payload
        ///     Timeout - A <see cref="System.TimeSpan"/> string that specifies that Work Item execution timeout
        /// </remarks>
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
                if (TimeSpan.TryParse(timeoutString, CultureInfo.InvariantCulture, out TimeSpan timeout))
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

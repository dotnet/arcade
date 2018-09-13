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
        ///   The 'source' value reported to Helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Source { get; set; }

        /// <summary>
        ///   The 'type' value reported to Helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Type { get; set; }

        /// <summary>
        ///   The 'build' value reported to Helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Build { get; set; }

        /// <summary>
        ///   The Helix queue this job should run on
        /// </summary>
        [Required]
        public string TargetQueue { get; set; }

        /// <summary>
        ///   When the task finishes, the correlation ID of the job that has been created
        /// </summary>
        [Output]
        public string JobCorrelationId { get; set; }

        /// <summary>
        ///   A set of directories or archives to be sent as correlation payloads for the Helix job.
        /// </summary>
        /// <remarks>
        ///   Metadata Used:
        ///     FullPath - This path is required to be a directory to be zipped up or an already-zipped archive
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
        ///     PayloadArchive - An archive that will be sent up as the Work Item payload
        ///     Timeout - A <see cref="System.TimeSpan"/> string that specifies that Work Item execution timeout
        /// </remarks>
        public ITaskItem[] WorkItems { get; set; }

        protected override async Task ExecuteCore()
        {
            IJobDefinition def = HelixApi.Job.Define()
                .WithSource(Source)
                .WithType(Type)
                .WithBuild(Build)
                .WithTargetQueue(TargetQueue);

            if (CorrelationPayloads != null)
            {
                foreach (ITaskItem correlationPayload in CorrelationPayloads)
                {
                    def = AddCorrelationPayload(def, correlationPayload);
                }
            }

            if (WorkItems != null)
            {
                foreach (ITaskItem workItem in WorkItems)
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

            ISentJob job = await def.SendAsync();
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

            IWorkItemDefinitionWithPayload wiWithPayload = def.DefineWorkItem(name)
                .WithCommand(command);

            string payloadDirectory = workItem.GetMetadata("PayloadDirectory");
            string payloadArchive = workItem.GetMetadata("PayloadArchive");
            IWorkItemDefinition wi;
            if (!string.IsNullOrEmpty(payloadDirectory))
            {
                wi = wiWithPayload.WithDirectoryPayload(payloadDirectory);
            }
            else if (!string.IsNullOrEmpty(payloadArchive))
            {
                wi = wiWithPayload.WithArchivePayload(payloadArchive);
            }
            else
            {
                wi = wiWithPayload.WithEmptyPayload();
            }

            string timeoutString = workItem.GetMetadata("Timeout");
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
            string path = correlationPayload.GetMetadata("FullPath");

            if (Directory.Exists(path))
            {
                return def.WithCorrelationPayloadDirectory(path);
            }
            else if (File.Exists(path))
            {
                return def.WithCorrelationPayloadArchive(path);
            }
            else
            {
                Log.LogError($"Correlation Payload '{path}' not found.");
                return def;
            }

        }
    }
}

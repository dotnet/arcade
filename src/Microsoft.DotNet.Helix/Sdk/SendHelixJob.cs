using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        ///   <see langword="true"/> when the work items are executing on a Posix shell; <see langword="false"/> otherwise.
        /// </summary>
        public bool IsPosixShell { get; set; }

        /// <summary>
        ///   When the task finishes, the correlation id of the job that has been created
        /// </summary>
        [Output]
        public string JobCorrelationId { get; set; }

        /// <summary>
        ///   A collection of commands that will run for each work item before any work item commands.
        ///   Use ';' to separate commands and escape a ';' with ';;'
        /// </summary>
        public string[] PreCommands { get; set; }

        /// <summary>
        ///   A collection of commands that will run for each work item after any work item commands.
        ///   Use ';' to separate commands and escape a ';' with ';;'
        /// </summary>
        public string[] PostCommands { get; set; }

        /// <summary>
        ///   A set of directories that will be zipped up and sent as Correlation Payloads for the helix job.
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
        ///     PreCommands
        ///       A collection of commands that will run for this work item before the 'Command' Runs
        ///       Use ';' to separate commands and escape a ';' with ';;'
        ///     PostCommands
        ///       A collection of commands that will run for this work item after the 'Command' Runs
        ///       Use ';' to separate commands and escape a ';' with ';;'
        /// </remarks>
        public ITaskItem[] WorkItems { get; set; }

        private CommandPayload _commandPayload;

        protected override async Task ExecuteCore()
        {
            using (_commandPayload = new CommandPayload(this))
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

                if (_commandPayload.TryGetPayloadDirectory(out string directory))
                {
                    def = def.WithCorrelationPayloadDirectory(directory);
                }

                // don't send the job if we have errors
                if (Log.HasLoggedErrors)
                {
                    return;
                }

                Log.LogMessage(MessageImportance.Normal, "Sending Job...");

                ISentJob job = await def.SendAsync();
                JobCorrelationId = job.CorrelationId;
            }
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

            Log.LogMessage(MessageImportance.Low, $"Adding work item '{name}'");

            var commands = GetCommands(workItem, command).ToList();

            IWorkItemDefinitionWithPayload wiWithPayload;

            if (commands.Count == 1)
            {
                wiWithPayload = def.DefineWorkItem(name).WithCommand(commands[0]);
                Log.LogMessage(MessageImportance.Low, $"  Command: '{commands[0]}'");
            }
            else
            {
                string commandFile = _commandPayload.AddCommandFile(commands);
                string helixCorrelationPayload =
                    IsPosixShell ? "$HELIX_CORRELATION_PAYLOAD/" : "%HELIX_CORRELATION_PAYLOAD%\\";
                wiWithPayload = def.DefineWorkItem(name).WithCommand(helixCorrelationPayload + commandFile);
                Log.LogMessage(MessageImportance.Low, $"  Command File: '{commandFile}'");
                foreach (string c in commands)
                {
                    Log.LogMessage(MessageImportance.Low, $"    {c}");
                }
            }

            string payloadDirectory = workItem.GetMetadata("PayloadDirectory");
            string payloadArchive = workItem.GetMetadata("PayloadArchive");
            IWorkItemDefinition wi;
            if (!string.IsNullOrEmpty(payloadDirectory))
            {
                wi = wiWithPayload.WithDirectoryPayload(payloadDirectory);
                Log.LogMessage(MessageImportance.Low, $"  Directory Payload: '{payloadDirectory}'");
            }
            else if (!string.IsNullOrEmpty(payloadArchive))
            {
                wi = wiWithPayload.WithArchivePayload(payloadArchive);
                Log.LogMessage(MessageImportance.Low, $"  Archive Payload: '{payloadArchive}'");
            }
            else
            {
                wi = wiWithPayload.WithEmptyPayload();
                Log.LogMessage(MessageImportance.Low, "  Empty Payload");
            }


            string timeoutString = workItem.GetMetadata("Timeout");
            if (!string.IsNullOrEmpty(timeoutString))
            {
                if (TimeSpan.TryParse(timeoutString, CultureInfo.InvariantCulture, out TimeSpan timeout))
                {
                    wi = wi.WithTimeout(timeout);
                    Log.LogMessage(MessageImportance.Low, $"  Timeout: '{timeout}'");
                }
                else
                {
                    Log.LogWarning($"Timeout value '{timeoutString}' could not be parsed.");
                }
            }
            else
            {
                Log.LogMessage(MessageImportance.Low, "  Default Timeout");
            }

            return wi.AttachToJob();
        }

        private IEnumerable<string> GetCommands(ITaskItem workItem, string workItemCommand)
        {
            if (PreCommands != null)
            {
                foreach (string command in PreCommands)
                {
                    yield return command;
                }
            }

            if (TryGetMetadata(workItem, "PreCommands", out string workItemPreCommandsString))
            {
                foreach (string command in SplitCommands(workItemPreCommandsString))
                {
                    yield return command;
                }
            }

            yield return workItemCommand;

            if (TryGetMetadata(workItem, "PostCommands", out string workItemPostCommandsString))
            {
                foreach (string command in SplitCommands(workItemPostCommandsString))
                {
                    yield return command;
                }
            }

            if (PostCommands != null)
            {
                foreach (string command in PostCommands)
                {
                    yield return command;
                }
            }
        }

        private IEnumerable<string> SplitCommands(string value)
        {
            var sb = new StringBuilder();

            using (var enumerator = value.GetEnumerator())
            {
                char prev = default;
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current != ';')
                    {
                        if (prev == ';' && sb.Length != 0)
                        {
                            var part = sb.ToString().Trim();
                            if (!string.IsNullOrEmpty(part))
                            {
                                yield return part;
                            }
                            sb.Length = 0;
                        }
                        sb.Append(enumerator.Current);
                    }
                    else if (enumerator.Current == ';')
                    {
                        if (prev == ';')
                        {
                            sb.Append(';');
                            prev = default;
                            continue;
                        }
                    }

                    prev = enumerator.Current;
                }
            }
            if (sb.Length != 0)
            {
                var part = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(part))
                {
                    yield return part;
                }
            }
        }

        private bool TryGetMetadata(ITaskItem item, string key, out string value)
        {
            value = item.GetMetadata(key);
            return !string.IsNullOrEmpty(value);
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
                Log.LogMessage(MessageImportance.Low, $"Adding Correlation Payload Directory '{path}'");
                return def.WithCorrelationPayloadDirectory(path);
            }
            else if (File.Exists(path))
            {
                Log.LogMessage(MessageImportance.Low, $"Adding Correlation Payload Archive '{path}'");
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

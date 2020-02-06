using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class SendHelixJob : HelixTask
    {
        /// <summary>
        ///   The 'type' value reported to Helix
        /// </summary>
        /// <remarks>
        ///   This value is used to filter and sort jobs on Mission Control
        /// </remarks>
        [Required]
        public string Type { get; set; }

        /// <summary>
        ///   The Helix queue this job should run on
        /// </summary>
        [Required]
        public string TargetQueue { get; set; }

        /// <summary>
        /// Required if sending anonymous, not allowed if authenticated
        /// The GitHub username of the job creator
        /// </summary>
        public string Creator { get; set; }

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
        ///   When the task finishes, the results container uri should be available in case we want to download files.
        /// </summary>
        [Output]
        public string ResultsContainerUri { get; set; }

        /// <summary>
        ///   If the job is internal, we need to give the DownloadFromResultsContainer task the Write SAS to download files.
        /// </summary>
        [Output]
        public string ResultsContainerReadSAS { get; set; }

        /// <summary>
        ///   A collection of commands that will run for each work item before any work item commands.
        ///   Use a semicolon to delimit these and escape semicolons by percent coding them ('%3B').
        ///   NOTE: This is different behavior from the WorkItem PreCommands, where semicolons are escaped
        ///   by using double semicolons (';;').
        /// </summary>
        public string[] PreCommands { get; set; }

        /// <summary>
        ///   A collection of commands that will run for each work item after any work item commands.
        ///   Use a semicolon to delimit these and escape semicolons by percent coding them ('%3B').
        ///   NOTE: This is different behavior from the WorkItem PostCommands, where semicolons are escaped
        ///   by using double semicolons (';;').
        /// </summary>
        public string[] PostCommands { get; set; }

        /// <summary>
        ///   A set of correlation payloads that will be sent for the helix job.
        /// </summary>
        /// <remarks>
        ///   These Items can be either:
        ///     A Directory - The specified directory will be zipped up and sent as a correlation payload
        ///     A File - The specified archive file will be sent as a correlation payload
        ///     A Uri - The Item's Uri metadata will be used as a correlation payload
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
        ///     NOTE: only a single Payload parameter should be used; they are not to be used in combination
        ///     PayloadDirectory - A directory that will be zipped up and sent as the Work Item payload
        ///     PayloadArchive - An archive that will be sent up as the Work Item payload
        ///     PayloadUri - An uri of the archive that will be sent up as the Work Item payload
        ///     Timeout - A <see cref="System.TimeSpan"/> string that specifies that Work Item execution timeout
        ///     PreCommands
        ///       A collection of commands that will run for this work item before the 'Command' Runs
        ///       Use ';' to separate commands and escape a ';' with ';;'
        ///       NOTE: This is different behavior from the Helix PreCommands, where semicolons are escaped
        ///       with percent coding.
        ///     PostCommands
        ///       A collection of commands that will run for this work item after the 'Command' Runs
        ///       Use ';' to separate commands and escape a ';' with ';;'
        ///       NOTE: This is different behavior from the Helix PostCommands, where semicolons are escaped
        ///       with percent coding.
        ///     Destination
        ///       The directory in which to unzip the correlation payload on the Helix agent
        /// </remarks>
        public ITaskItem[] WorkItems { get; set; }

        /// <summary>
        ///  A set of properties for helix to map the job using architecture and configuration
        /// </summary>
        /// <remarks>
        ///  Required Metadata:
        ///     Identity - The property Key
        ///     Value - The property Value mapped to the key.
        /// </remarks>
        public ITaskItem[] HelixProperties { get; set; }

        /// <summary>
        /// Max automatic retry of workitems which do not return 0
        /// </summary>
        public int MaxRetryCount { get; set; }

        private CommandPayload _commandPayload;

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(AccessToken) && string.IsNullOrEmpty(Creator))
            {
                Log.LogError(FailureCategory.Build, "Creator is required when using anonymous access.");
                return;
            }

            if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Creator))
            {
                Log.LogError(FailureCategory.Build, "Creator is forbidden when using authenticated access.");
                return;
            }

            Type = Type.ToLowerInvariant();

            cancellationToken.ThrowIfCancellationRequested();

            using (_commandPayload = new CommandPayload(this))
            {
                var currentHelixApi = HelixApi;

                IJobDefinition def = currentHelixApi.Job.Define()
                    .WithType(Type)
                    .WithTargetQueue(TargetQueue)
                    .WithMaxRetryCount(MaxRetryCount);
                Log.LogMessage($"Initialized job definition with type '{Type}', and target queue '{TargetQueue}'");

                if (!string.IsNullOrEmpty(Creator))
                {
                    def = def.WithCreator(Creator);
                    Log.LogMessage($"Setting creator to '{Creator}'");
                }

                Log.LogMessage(MessageImportance.High, $"Uploading payloads for Job on {TargetQueue}...");

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
                    Log.LogError(FailureCategory.Build, "SendHelixJob given no WorkItems to send.");
                }

                if (_commandPayload.TryGetPayloadDirectory(out string directory))
                {
                    def = def.WithCorrelationPayloadDirectory(directory);
                }

                Log.LogMessage(MessageImportance.High, $"Finished uploading payloads for Job on {TargetQueue}...");

                if (HelixProperties != null)
                {
                    foreach (ITaskItem helixProperty in HelixProperties)
                    {
                        def = AddProperty(def, helixProperty);
                    }
                }

                // don't send the job if we have errors
                if (Log.HasLoggedErrors)
                {
                    return;
                }

                Log.LogMessage(MessageImportance.High, $"Sending Job to {TargetQueue}...");

                cancellationToken.ThrowIfCancellationRequested();
                ISentJob job = await def.SendAsync(msg => Log.LogMessage(msg), cancellationToken);
                JobCorrelationId = job.CorrelationId;
                ResultsContainerUri = job.ResultsContainerUri;
                ResultsContainerReadSAS = job.ResultsContainerReadSAS;
                cancellationToken.ThrowIfCancellationRequested();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private IJobDefinition AddProperty(IJobDefinition def, ITaskItem property)
        {
            if (!property.GetRequiredMetadata(Log, "Identity", out string key))
            {
                return def;
            }

            if (!property.GetRequiredMetadata(Log, "Value", out string value))
            {
                return def;
            }

            def.WithProperty(key, value);
            Log.LogMessage($"Added property '{key}' (value: '{value}') to job definition.");
            return def;
        }

        private IJobDefinition AddWorkItem(IJobDefinition def, ITaskItem workItem)
        {
            if (!workItem.GetRequiredMetadata(Log, "Identity", out string name))
            {
                return def;
            }

            if(name.Contains('%'))
            {
                Log.LogWarning($"Work Item named '{name}' contains encoded characters which is not recommended.");
            }

            var cleanedName = Helpers.CleanWorkItemName(name);

            if (name != cleanedName)
            {
                Log.LogWarning($"Work Item named '{name}' contains unsupported characters and has been renamed to '{cleanedName}'.");
            }

            name = cleanedName;

            if (!workItem.GetRequiredMetadata(Log, "Command", out string command))
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
            string payloadUri = workItem.GetMetadata("PayloadUri");
            IWorkItemDefinition wi;
            if (!string.IsNullOrEmpty(payloadUri))
            {
                wi = wiWithPayload.WithPayloadUri(new Uri(payloadUri));
                Log.LogMessage(MessageImportance.Low, $"  Uri Payload: '{payloadUri}'");
            }
            else if (!string.IsNullOrEmpty(payloadDirectory))
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

            if (workItem.TryGetMetadata("PreCommands", out string workItemPreCommandsString))
            {
                foreach (string command in SplitCommands(workItemPreCommandsString))
                {
                    yield return command;
                }
            }

            yield return workItemCommand;

            string exitCodeVariableName = "_commandExitCode";

            // Capture helix command exit code, in case work item command (i.e xunit call) exited with a failure,
            // this way we can exit the process honoring that exit code, needed for retry.
            yield return IsPosixShell ? $"export {exitCodeVariableName}=$?" : $"set {exitCodeVariableName}=%ERRORLEVEL%";

            if (workItem.TryGetMetadata("PostCommands", out string workItemPostCommandsString))
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

            // Exit with the captured exit code from workitem command.
            yield return IsPosixShell ? $"exit ${exitCodeVariableName}" : $"EXIT /b %{exitCodeVariableName}%";
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

        private IJobDefinition AddCorrelationPayload(IJobDefinition def, ITaskItem correlationPayload)
        {
            string path = correlationPayload.GetMetadata("FullPath");
            string uri = correlationPayload.GetMetadata("Uri");
            string destination = correlationPayload.GetMetadata("Destination") ?? "";

            if (!string.IsNullOrEmpty(uri))
            {
                Log.LogMessage(MessageImportance.Low, $"Adding Correlation Payload URI '{uri}', destination '{destination}'");

                if (!string.IsNullOrEmpty(destination))
                {
                    return def.WithCorrelationPayloadUris(new Dictionary<Uri, string>() { { new Uri(uri), destination } });
                }
                else
                {
                    return def.WithCorrelationPayloadUris(new Uri(uri));
                }
            }

            if (Directory.Exists(path))
            {
                string includeDirectoryNameStr = correlationPayload.GetMetadata("IncludeDirectoryName");
                bool.TryParse(includeDirectoryNameStr, out bool includeDirectoryName);

                Log.LogMessage(MessageImportance.Low, $"Adding Correlation Payload Directory '{path}', destination '{destination}'");
                return def.WithCorrelationPayloadDirectory(path, includeDirectoryName, destination);
            }

            if (File.Exists(path))
            {
                Log.LogMessage(MessageImportance.Low, $"Adding Correlation Payload Archive '{path}', destination '{destination}'");
                return def.WithCorrelationPayloadArchive(path, destination);
            }

            Log.LogError(FailureCategory.Build, $"Correlation Payload '{path}' not found.");
            return def;
        }
    }
}

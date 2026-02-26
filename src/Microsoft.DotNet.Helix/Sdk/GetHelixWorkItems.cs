// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class GetHelixWorkItems : HelixTask
    {
        public const int DelayBetweenHelixApiCallsInMs = 500;

        /// <summary>
        /// An array of Helix Jobs for which to get status
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        [Output]
        public ITaskItem[] WorkItems { get; set; }

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
        {
            WorkItems = (await Task.WhenAll(Jobs.Select(j => GetWorkItemsAsync(j, cancellationToken))).ConfigureAwait(false)).SelectMany(r => r).ToArray();
        }

        private async Task<IEnumerable<ITaskItem>> GetWorkItemsAsync(ITaskItem job, CancellationToken cancellationToken)
        {
            var jobName = job.GetMetadata("Identity");

            Log.LogMessage($"Getting status of job {jobName}");

            var status = await HelixApi.Job.PassFailAsync(jobName, cancellationToken).ConfigureAwait(false);

            if (status.Working > 0)
            {
                Log.LogError(
                    FailureCategory.Build,
                    $"This task can only be used on completed jobs. There are {status.Working} of {status.Total} unfinished work items.");
                return Array.Empty<ITaskItem>();
            }

            var workItems = new List<ITaskItem>();

            IDictionary<string, string> CreateWorkItemMetadata(string name)
            {
                var metadata = job.CloneCustomMetadata() as IDictionary<string, string>;
                metadata["JobName"] = jobName;
                metadata["WorkItemName"] = name;
                var consoleUri = HelixApi.Options.BaseUri.AbsoluteUri.TrimEnd('/') + $"/api/2019-06-17/jobs/{jobName}/workitems/{Uri.EscapeDataString(name)}/console";
                metadata["ConsoleOutputUri"] = consoleUri;

                return metadata;
            }

            ITaskItem2 CreateTaskItem(string workItemName, IDictionary<string, string> metadata)
            {
                ITaskItem2 workItem = new TaskItem(workItemName);

                foreach(KeyValuePair<string, string> entry in metadata)
                {
                    workItem.SetMetadataValueLiteral(entry.Key, entry.Value);
                }

                return workItem;
            }

            foreach (string name in status.Passed)
            {
                string wi = Helpers.CleanWorkItemName(name);

                var metadata = CreateWorkItemMetadata(wi);
                metadata["Failed"] = "false";

                workItems.Add(CreateTaskItem($"{jobName}/{wi}", metadata));
            }

            foreach (string name in status.Failed)
            {
                string wi = Helpers.CleanWorkItemName(name);

                var metadata = CreateWorkItemMetadata(wi);
                metadata["Failed"] = "true";

                try
                {
                    // Do this serially with a delay because total failure can hit throttling
                    // latestOnly parameter is set false here to download all possible files
                    var files = await HelixApi.WorkItem.ListFilesAsync(wi, jobName, false, cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(AccessToken))
                    {
                        // Add AccessToken to all file links because the api requires auth if we submitted the job with auth
                        files = files
                                .Select(file => new UploadedFile(file.Name, file.Link + "?access_token=" + AccessToken))
                                .ToImmutableList();
                    }

                    metadata["UploadedFiles"] = JsonConvert.SerializeObject(files);
                }
                catch (Exception ex)
                {
                    Log.LogWarningFromException(ex);
                }

                // Fetch console output and work item details for failed items so we can surface
                // actual error messages instead of the generic "work item failed" text.
                try
                {
                    var details = await HelixApi.WorkItem.DetailsAsync(wi, jobName, cancellationToken).ConfigureAwait(false);
                    if (details.ExitCode.HasValue)
                    {
                        metadata["ExitCode"] = details.ExitCode.Value.ToString();
                    }
                    if (details.Errors != null && details.Errors.Count > 0)
                    {
                        metadata["HelixErrors"] = JsonConvert.SerializeObject(details.Errors.Select(e => e.Message));
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.Low, $"Failed to get work item details for {wi}: {ex.Message}");
                }

                try
                {
                    using (var consoleStream = await HelixApi.WorkItem.ConsoleLogAsync(wi, jobName, cancellationToken).ConfigureAwait(false))
                    using (var reader = new StreamReader(consoleStream, Encoding.UTF8))
                    {
                        string fullConsole = await reader.ReadToEndAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(fullConsole))
                        {
                            // Extract the tail of the console output (last ~100 lines) to capture errors near the crash
                            string[] lines = fullConsole.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            int startIndex = Math.Max(0, lines.Length - 100);
                            string tail = string.Join(Environment.NewLine, lines, startIndex, lines.Length - startIndex);

                            // Also try to extract specific test failure messages from the console output
                            string errorExcerpt = ExtractTestFailureMessages(fullConsole, tail);
                            metadata["ConsoleErrorText"] = errorExcerpt;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.Low, $"Failed to fetch console output for {wi}: {ex.Message}");
                }

                workItems.Add(CreateTaskItem($"{jobName}/{wi}", metadata));
                await Task.Delay(DelayBetweenHelixApiCallsInMs, cancellationToken);
            }

            return workItems;
        }

        /// <summary>
        /// Extracts actionable test failure messages from console output.
        /// Looks for common patterns from dotnet test / xUnit / MSTest failure output.
        /// Falls back to the tail of the console if no specific patterns are found.
        /// </summary>
        private static string ExtractTestFailureMessages(string fullConsole, string tail)
        {
            var sb = new StringBuilder();

            // Look for "Failed" test result lines from dotnet test console output
            // Pattern: "  Failed TestClassName.MethodName [duration]"
            string[] allLines = fullConsole.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inFailureBlock = false;
            int failureLineCount = 0;
            const int maxFailureLines = 30; // Cap per failure block to avoid giant output
            int totalFailuresFound = 0;
            const int maxTotalFailures = 5; // Only show first N failures

            foreach (string line in allLines)
            {
                string trimmed = line.TrimStart();

                // Detect start of a failure block
                if (trimmed.StartsWith("Failed ", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Error Message:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Assert.", StringComparison.OrdinalIgnoreCase))
                {
                    if (!inFailureBlock)
                    {
                        totalFailuresFound++;
                        if (totalFailuresFound > maxTotalFailures)
                        {
                            sb.AppendLine($"... and more failures (showing first {maxTotalFailures})");
                            break;
                        }
                        inFailureBlock = true;
                        failureLineCount = 0;
                        if (sb.Length > 0)
                            sb.AppendLine();
                    }
                }

                if (inFailureBlock)
                {
                    sb.AppendLine(line);
                    failureLineCount++;

                    // End the block after enough context or on a blank-ish line after error content
                    if (failureLineCount >= maxFailureLines ||
                        (failureLineCount > 3 && string.IsNullOrWhiteSpace(trimmed)))
                    {
                        inFailureBlock = false;
                    }
                }
            }

            // If we found structured failure messages, return them
            if (sb.Length > 0)
            {
                return sb.ToString().TrimEnd();
            }

            // Otherwise, return the tail of the console output as-is
            // Truncate to ~4000 chars to fit in AzDO errorMessage field
            if (tail.Length > 4000)
            {
                tail = "..." + tail.Substring(tail.Length - 4000);
            }
            return tail;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class CheckHelixJobStatus : HelixTask
    {
        /// <summary>
        /// An array of Helix Jobs to be checked
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        [Required]
        public ITaskItem[] WorkItems { get; set; }

        public bool FailOnWorkItemFailure { get; set; } = true;

        public bool FailOnMissionControlTestFailure { get; set; } = false;

        protected override Task ExecuteCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FailOnWorkItemFailure)
            {
                string accessTokenSuffix = string.IsNullOrEmpty(AccessToken) ? "" : "?access_token={Get this from helix.dot.net}";
                foreach (ITaskItem workItem in WorkItems)
                {
                    var failed = workItem.GetMetadata("Failed");
                    if (failed == "true")
                    {
                        var jobName = workItem.GetMetadata("JobName");
                        var workItemName = workItem.GetMetadata("WorkItemName");
                        var consoleUri = workItem.GetMetadata("ConsoleOutputUri");
                        var exitCode = workItem.GetMetadata("ExitCode");
                        var consoleErrorText = workItem.GetMetadata("ConsoleErrorText");

                        var sb = new System.Text.StringBuilder();
                        sb.Append($"Work item {workItemName} in job {jobName} has failed.");

                        if (!string.IsNullOrEmpty(exitCode))
                        {
                            sb.Append($" (exit code {exitCode})");
                        }

                        sb.AppendLine();
                        sb.AppendLine($"Failure log: {consoleUri}{accessTokenSuffix}");

                        if (!string.IsNullOrEmpty(consoleErrorText))
                        {
                            sb.AppendLine();
                            sb.AppendLine("Error details:");
                            // Truncate to avoid excessively long MSBuild error messages
                            string truncated = consoleErrorText.Length > 2000
                                ? consoleErrorText.Substring(0, 2000) + "..."
                                : consoleErrorText;
                            sb.Append(truncated);
                        }

                        Log.LogError(FailureCategory.Test, sb.ToString());
                    }
                }
            }

            if (FailOnMissionControlTestFailure)
            {
                Log.LogMessage($"Mission Control is deprecated. Please set FailOnMissionControlTestFailure to false.");
            }

            return Task.CompletedTask;
        }
    }
}

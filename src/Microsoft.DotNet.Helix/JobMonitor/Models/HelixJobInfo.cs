// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor.Models
{
    /// <summary>
    /// Represents a Helix job and its current status.
    /// Decoupled from the Helix Client SDK's generated models.
    /// </summary>
    public sealed class HelixJobInfo
    {
        public const string PreviousHelixJobNamePropertyName = "PreviousHelixJobName";

        public HelixJobInfo(JobSummary helixJob)
        {
            JobName = helixJob.Name;
            Status = helixJob.Finished != null ? "finished" : "running";
            TestRunName = GetTestRunNameFromJob(helixJob);
            StageName = GetStringPropertyFromJob(helixJob, "System.StageName");
            Properties = helixJob.Properties;
        }

        public HelixJobInfo(
            string jobName,
            string status,
            string testRunName = null,
            string stageName = null,
            string submitterJobName = null,
            string previousHelixJobName = null)
        {
            JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
            Status = status ?? throw new ArgumentNullException(nameof(status));
            TestRunName = testRunName;
            StageName = stageName;
            Properties = CreateProperties(testRunName, stageName, submitterJobName, previousHelixJobName);
        }

        public string JobName { get; }

        public string Status { get; }

        /// <summary>
        /// The desired AzDO test run name for this job. May come from a Helix job property.
        /// Falls back to the job name if not set.
        /// </summary>
        public string TestRunName { get; }

        /// <summary>
        /// Name of the Azure DevOps pipeline stage that submitted this Helix job, taken from
        /// the "System.StageName" property stamped onto the job by <c>SendHelixJob</c>. May be
        /// null if the property is not present.
        /// </summary>
        public string StageName { get; }

        public string SubmitterJobName => GetStringProperty(Properties, "System.JobName");

        public string PreviousHelixJobName => GetStringProperty(Properties, PreviousHelixJobNamePropertyName);

        public JToken Properties { get; }

        public bool IsCompleted => Status.Equals("finished", StringComparison.OrdinalIgnoreCase)
            || Status.Equals("failed", StringComparison.OrdinalIgnoreCase);

        private static string GetTestRunNameFromJob(JobSummary helixJob)
        {
            // The Helix SDK stamps the desired Azure DevOps test run name onto the job as a
            // "TestRunName" property when submitting (matching what StartAzurePipelinesTestRun
            // would have used). Fall back to the Helix job name if the property is missing so
            // we always produce a non-empty name.
            if (helixJob.Properties is JObject properties)
            {
                if (properties.TryGetValue("TestRunName", out JToken testRunName))
                {
                    string value = testRunName?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                properties.TryGetValue("System.PhaseName", out JToken phaseName);
                properties.TryGetValue("System.JobName", out JToken jobName);
                return $"{phaseName} {jobName} run on {helixJob.QueueId}".Trim();
            }

            return helixJob.Name;
        }

        private static string GetStringPropertyFromJob(JobSummary helixJob, string propertyName)
            => GetStringProperty(helixJob.Properties, propertyName);

        private static string GetStringProperty(JToken propertiesToken, string propertyName)
        {
            if (propertiesToken is JObject properties
                && properties.TryGetValue(propertyName, out JToken token))
            {
                string value = token?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static JObject CreateProperties(
            string testRunName,
            string stageName,
            string submitterJobName,
            string previousHelixJobName)
        {
            var properties = new JObject();

            if (!string.IsNullOrEmpty(testRunName))
            {
                properties["TestRunName"] = testRunName;
            }

            if (!string.IsNullOrEmpty(stageName))
            {
                properties["System.StageName"] = stageName;
            }

            if (!string.IsNullOrEmpty(submitterJobName))
            {
                properties["System.JobName"] = submitterJobName;
            }

            if (!string.IsNullOrEmpty(previousHelixJobName))
            {
                properties[PreviousHelixJobNamePropertyName] = previousHelixJobName;
            }

            return properties;
        }
    }
}

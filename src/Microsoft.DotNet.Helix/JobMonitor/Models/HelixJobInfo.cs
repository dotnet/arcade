// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.Helix.JobMonitor.Models
{
    /// <summary>
    /// Represents a Helix job and its current status.
    /// Decoupled from the Helix Client SDK's generated models.
    /// </summary>
    public sealed class HelixJobInfo
    {
        public HelixJobInfo(string jobName, string status, string testRunName = null)
        {
            JobName = jobName ?? throw new ArgumentNullException(nameof(jobName));
            Status = status ?? throw new ArgumentNullException(nameof(status));
            TestRunName = testRunName;
        }

        public string JobName { get; }

        public string Status { get; }

        /// <summary>
        /// The desired AzDO test run name for this job. May come from a Helix job property.
        /// Falls back to the job name if not set.
        /// </summary>
        public string TestRunName { get; }

        public bool IsCompleted => Status.Equals("finished", StringComparison.OrdinalIgnoreCase)
            || Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
    }
}

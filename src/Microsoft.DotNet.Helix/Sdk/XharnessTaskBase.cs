using System;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public abstract class XHarnessTaskBase : BaseTask
    {
        private const int DefaultWorkItemTimeoutInMinutes = 20;
        private const int DefaultTestTimeoutInMinutes = 12;

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// Optional timeout for the actual test execution in the TimeSpan format (e.g. 00:45:00 for 45 minutes).
        /// Defaults to 00:12:00.
        /// </summary>
        public string TestTimeout { get; set; }

        /// <summary>
        /// Optional timeout for the whole Helix work item run (includes SDK and tool installation)
        /// in the TimeSpan format (e.g. 00:45:00 for 45 minutes).
        /// Defaults to 00:20:00.
        /// </summary>
        public string WorkItemTimeout { get; set; }

        /// <summary>
        /// Extra arguments that will be passed to the iOS/Android/... app that is being run
        /// </summary>
        public string AppArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] WorkItems { get; set; }

        protected (TimeSpan TestTimeout, TimeSpan WorkItemTimeout) ParseTimeouts()
        {
            TimeSpan testTimeout = TimeSpan.FromMinutes(DefaultTestTimeoutInMinutes);
            if (!string.IsNullOrEmpty(TestTimeout))
            {
                if (!TimeSpan.TryParse(TestTimeout, out testTimeout) || testTimeout.Ticks < 0)
                {
                    Log.LogWarning($"Invalid value \"{TestTimeout}\" provided in TestTimeout; " +
                        $"falling back to default value of \"00:{DefaultTestTimeoutInMinutes}:00\" ({DefaultTestTimeoutInMinutes} minutes)");
                    testTimeout = TimeSpan.FromMinutes(DefaultTestTimeoutInMinutes);
                }
            }

            TimeSpan workItemTimeout = TimeSpan.FromMinutes(DefaultWorkItemTimeoutInMinutes);
            if (!string.IsNullOrEmpty(WorkItemTimeout))
            {
                if (!TimeSpan.TryParse(WorkItemTimeout, out workItemTimeout) || workItemTimeout.Ticks < 0)
                {
                    Log.LogWarning($"Invalid value \"{WorkItemTimeout}\" provided in WorkItemTimeout; " +
                        $"falling back to default value of \"00:{DefaultWorkItemTimeoutInMinutes}:00\" ({DefaultWorkItemTimeoutInMinutes} minutes)");
                    workItemTimeout = TimeSpan.FromMinutes(DefaultWorkItemTimeoutInMinutes);
                }
            }
            else if (!string.IsNullOrEmpty(TestTimeout))
            {
                // When test timeout was set and work item timeout has not,
                // we adjust the work item timeout to give enough space for things to work
                workItemTimeout = TimeSpan.FromMinutes(testTimeout.TotalMinutes + DefaultWorkItemTimeoutInMinutes - DefaultTestTimeoutInMinutes);
            }

            if (workItemTimeout <= testTimeout)
            {
                Log.LogWarning(
                    $"Work item timeout ({workItemTimeout}) should be larger than test timeout ({testTimeout}) " +
                    $"to allow the XHarness tool to be initialized properly.");
            }

            return (TestTimeout: testTimeout, WorkItemTimeout: workItemTimeout);
        }
    }
}

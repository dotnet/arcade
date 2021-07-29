using System;
using System.Collections.Generic;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public abstract class XHarnessTaskBase : MSBuildTaskBase
    {
        private static readonly TimeSpan s_defaultWorkItemTimeout = TimeSpan.FromMinutes(20);
        private static readonly TimeSpan s_defaultTestTimeout = TimeSpan.FromMinutes(12);

        public class MetadataName
        {
            public const string TestTimeout = "TestTimeout";
            public const string WorkItemTimeout = "WorkItemTimeout";
            public const string ExpectedExitCode = "ExpectedExitCode";
            public const string CustomCommands = "CustomCommands";
        }

        protected const string ScriptNamespace = "tools.xharness_runner.";
        protected const string CustomCommandsScript = "command";

        /// <summary>
        /// Extra arguments that will be passed to the iOS/Android/... app that is being run
        /// </summary>
        public string AppArguments { get; set; }

        /// <summary>
        /// An array of ITaskItems of type HelixWorkItem
        /// </summary>
        [Output]
        public ITaskItem[] WorkItems { get; set; }

        /// <summary>
        /// Parses metadata of the task item pointing to the app (app bundle/apk) we want to turn into an XHarness job.
        /// </summary>
        /// <param name="xHarnessAppItem">MSBuild task item</param>
        /// <returns>
        /// Parsed data:
        ///   - TestTimeout - Optional timeout for the actual test execution
        ///   - WorkItemTimeout - Optional timeout for the whole Helix work item run (includes SDK and tool installation)
        ///   - ExpectedExitCode - Optional expected exit code parameter that is forwarded to XHarness
        /// </returns>
        protected (TimeSpan TestTimeout, TimeSpan WorkItemTimeout, int ExpectedExitCode, string CustomCommands) ParseMetadata(ITaskItem xHarnessAppItem)
        {
            xHarnessAppItem.TryGetMetadata(MetadataName.CustomCommands, out string customCommands);
            if (string.IsNullOrEmpty(customCommands))
            {
                customCommands = null;
            }

            // Optional timeout for the actual test execution in the TimeSpan format
            TimeSpan testTimeout = s_defaultTestTimeout;
            if (xHarnessAppItem.TryGetMetadata(MetadataName.TestTimeout, out string testTimeoutProp))
            {
                if (!TimeSpan.TryParse(testTimeoutProp, out testTimeout) || testTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{testTimeoutProp}\" provided in <{MetadataName.TestTimeout}>");
                }
            }

            // Optional timeout for the whole Helix work item run (includes SDK and tool installation)
            TimeSpan workItemTimeout = s_defaultWorkItemTimeout;
            if (xHarnessAppItem.TryGetMetadata(MetadataName.WorkItemTimeout, out string workItemTimeoutProp))
            {
                if (!TimeSpan.TryParse(workItemTimeoutProp, out workItemTimeout) || workItemTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{workItemTimeoutProp}\" provided in <{MetadataName.WorkItemTimeout}>");
                }
            }
            else if (!string.IsNullOrEmpty(testTimeoutProp))
            {
                // When test timeout was set and work item timeout has not,
                // we adjust the work item timeout to give enough space for things to work
                workItemTimeout = testTimeout + s_defaultWorkItemTimeout - s_defaultTestTimeout;
            }

            if (customCommands == null && workItemTimeout <= testTimeout)
            {
                Log.LogWarning(
                    $"Work item timeout ({workItemTimeout}) should be larger than test timeout ({testTimeout}) " +
                    $"to allow the XHarness tool to be initialized properly.");
            }

            int expectedExitCode = 0;
            if (xHarnessAppItem.TryGetMetadata(MetadataName.ExpectedExitCode, out string expectedExitCodeProp))
            {
                int.TryParse(expectedExitCodeProp, out expectedExitCode);
            }

            return (testTimeout, workItemTimeout, expectedExitCode, customCommands);
        }

        protected Build.Utilities.TaskItem CreateTaskItem(string workItemName, string payloadArchivePath, string command, TimeSpan timeout) =>
            new (workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", payloadArchivePath },
                { "Command", command },
                { "Timeout", timeout.ToString() },
            });
    }
}

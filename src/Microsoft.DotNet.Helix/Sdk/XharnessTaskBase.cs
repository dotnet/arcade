using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
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

        private const string TestTimeoutPropName = "TestTimeout";
        private const string WorkItemTimeoutPropName = "WorkItemTimeout";
        private const string ExpectedExitCodePropName = "ExpectedExitCode";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

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
        protected (TimeSpan TestTimeout, TimeSpan WorkItemTimeout, int ExpectedExitCode) ParseMetadata(ITaskItem xHarnessAppItem)
        {
            // Optional timeout for the actual test execution in the TimeSpan format
            TimeSpan testTimeout = TimeSpan.FromMinutes(DefaultTestTimeoutInMinutes);
            if (xHarnessAppItem.TryGetMetadata(TestTimeoutPropName, out string testTimeoutProp))
            {
                if (!TimeSpan.TryParse(testTimeoutProp, out testTimeout) || testTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{testTimeoutProp}\" provided in <{TestTimeoutPropName}>");
                }
            }

            // Optional timeout for the whole Helix work item run (includes SDK and tool installation)
            TimeSpan workItemTimeout = TimeSpan.FromMinutes(DefaultWorkItemTimeoutInMinutes);
            if (xHarnessAppItem.TryGetMetadata(WorkItemTimeoutPropName, out string workItemTimeoutProp))
            {
                if (!TimeSpan.TryParse(workItemTimeoutProp, out workItemTimeout) || workItemTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{workItemTimeoutProp}\" provided in <{WorkItemTimeoutPropName}>");
                }
            }
            else if (!string.IsNullOrEmpty(testTimeoutProp))
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

            int expectedExitCode = 0;
            if (xHarnessAppItem.TryGetMetadata(ExpectedExitCodePropName, out string expectedExitCodeProp))
            {
                int.TryParse(expectedExitCodeProp, out expectedExitCode);
            }

            return (
                TestTimeout: testTimeout,
                WorkItemTimeout: workItemTimeout,
                ExpectedExitCode: expectedExitCode);
        }

        protected static async Task AddResourceFileToPayload(string payloadArchivePath, string resourceFileName, string targetFileName = null)
        {
            using Stream fileStream = GetResourceFileContent(resourceFileName);
            await AddToPayloadArchive(payloadArchivePath, targetFileName ?? resourceFileName, fileStream);
        }

        protected static async Task AddToPayloadArchive(string payloadArchivePath, string targetFilename, Stream content)
        {
            using FileStream archiveStream = new FileStream(payloadArchivePath, FileMode.Open);
            using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.CreateEntry(targetFilename);
            using Stream targetStream = entry.Open();
            await content.CopyToAsync(targetStream);
        }

        protected static Stream GetResourceFileContent(string resourceFileName)
        {
            Assembly thisAssembly = typeof(XHarnessTaskBase).Assembly;
            return thisAssembly.GetManifestResourceStream($"{thisAssembly.GetName().Name}.tools.xharness_runner.{resourceFileName}");
        }
    }
}

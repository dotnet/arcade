using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided iOS app bundle paths.
    /// </summary>
    public class CreateXHarnessiOSWorkItems : XHarnessTaskBase
    {
        private const string EntryPointScriptName = "xharness-helix-job.ios.sh";
        private const string RunnerScriptName = "xharness-runner.ios.sh";
        private const int DefaultLaunchTimeoutInMinutes = 10;
        private const string LaunchTimeoutPropName = "LaunchTimeout";
        private const string TargetsPropName = "Targets";
        private const string IncludesTestRunnerPropName = "IncludesTestRunner";

        /// <summary>
        /// An array of one or more paths to iOS app bundles (folders ending with ".app" usually)
        /// that will be used to create Helix work items.
        /// </summary>
        public ITaskItem[] AppBundles { get; set; }

        /// <summary>
        /// Xcode version to use in the [major].[minor] format, e.g. 11.4
        /// </summary>
        public string XcodeVersion { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItems
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation</returns>
        public override bool Execute()
        {
            if (!IsPosixShell)
            {
                Log.LogError("IsPosixShell was specified as false for an iOS work item; these can only run on MacOS devices currently.");
                return false;
            }

            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Create work items for XHarness test execution
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            WorkItems = (await Task.WhenAll(AppBundles.Select(PrepareWorkItem))).Where(wi => wi != null).ToArray();
        }

        /// <summary>
        /// Prepares HelixWorkItem that can run on an iOS device using XHarness
        /// </summary>
        /// <param name="appFolderPath">Path to application package</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(ITaskItem appBundleItem)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            string appFolderPath = appBundleItem.ItemSpec.TrimEnd(Path.DirectorySeparatorChar);
            
            string workItemName = Path.GetFileName(appFolderPath);
            if (workItemName.EndsWith(".app"))
            {
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            var (testTimeout, workItemTimeout, expectedExitCode) = ParseMetadata(appBundleItem);

            // Validation of any metadata specific to iOS stuff goes here
            if (!appBundleItem.TryGetMetadata(TargetsPropName, out string targets))
            {
                Log.LogError("'Targets' metadata must be specified - " +
                    "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                return null;
            }

            // Optional timeout for the how long it takes for the app to be installed, booted and tests start executing
            TimeSpan launchTimeout = TimeSpan.FromMinutes(DefaultLaunchTimeoutInMinutes);
            if (appBundleItem.TryGetMetadata(LaunchTimeoutPropName, out string launchTimeoutProp))
            {
                if (!TimeSpan.TryParse(launchTimeoutProp, out launchTimeout) || launchTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{launchTimeoutProp}\" provided in <{LaunchTimeoutPropName}>");
                    return null;
                }
            }

            bool includesTestRunner = true;
            if (appBundleItem.TryGetMetadata(IncludesTestRunnerPropName, out string includesTestRunnerProp))
            {
                if (includesTestRunnerProp.ToLowerInvariant() == "false")
                {
                    includesTestRunner = false;
                }
            }

            if (includesTestRunner && expectedExitCode != 0)
            {
                Log.LogWarning("The ExpectedExitCode property is ignored in the `ios test` scenario");
            }

            string appName = Path.GetFileName(appBundleItem.ItemSpec);
            string command = GetHelixCommand(appName, targets, testTimeout, launchTimeout, includesTestRunner, expectedExitCode);

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appFolderPath}, Command: {command}");

            return new Microsoft.Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", await CreateZipArchiveOfFolder(appFolderPath) },
                { "Command", command },
                { "Timeout", workItemTimeout.ToString() },
            });
        }

        private string GetHelixCommand(string appName, string targets, TimeSpan testTimeout, TimeSpan launchTimeout, bool includesTestRunner, int expectedExitCode) =>
            $"chmod +x {EntryPointScriptName} && ./{EntryPointScriptName} " +
            $"--app \"$HELIX_WORKITEM_ROOT/{appName}\" " +
             "--output-directory \"$HELIX_WORKITEM_UPLOAD_ROOT\" " +
            $"--targets \"{targets}\" " +
            $"--timeout \"{testTimeout}\" " +
            $"--launch-timeout \"{launchTimeout}\" " +
             "--xharness-cli-path \"$XHARNESS_CLI_PATH\" " +
             "--command " + (includesTestRunner ? "test" : "run") +
            (expectedExitCode != 0 ? $" --expected-exit-code \"{expectedExitCode}\"" : string.Empty) +
            (!string.IsNullOrEmpty(XcodeVersion) ? $" --xcode-version \"{XcodeVersion}\"" : string.Empty) +
            (!string.IsNullOrEmpty(AppArguments) ? $" --app-arguments \"{AppArguments}\"" : string.Empty);

        private async Task<string> CreateZipArchiveOfFolder(string folderToZip)
        {
            if (!Directory.Exists(folderToZip))
            {
                Log.LogError($"Cannot find path containing app: '{folderToZip}'");
                return string.Empty;
            }

            string appFolderDirectory = Path.GetDirectoryName(folderToZip);
            string fileName = $"xharness-ios-app-payload-{Path.GetFileName(folderToZip).ToLowerInvariant()}.zip";
            string outputZipPath = Path.Combine(appFolderDirectory, fileName);

            if (File.Exists(outputZipPath))
            {
                Log.LogMessage($"Zip archive '{outputZipPath}' already exists, overwriting..");
                File.Delete(outputZipPath);
            }

            ZipFile.CreateFromDirectory(folderToZip, outputZipPath, CompressionLevel.Fastest, includeBaseDirectory: true);

            Log.LogMessage($"Adding the Helix job payload scripts into the ziparchive");
            await AddFileToPayload(outputZipPath, EntryPointScriptName);
            await AddFileToPayload(outputZipPath, RunnerScriptName);

            return outputZipPath;
        }

        private async Task AddFileToPayload(string payloadArchivePath, string fileName)
        {
            var thisAssembly = typeof(CreateXHarnessiOSWorkItems).Assembly;
            using Stream fileStream = thisAssembly.GetManifestResourceStream($"{thisAssembly.GetName().Name}.tools.xharness_runner.{fileName}");
            using FileStream archiveStream = new FileStream(payloadArchivePath, FileMode.Open);
            using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.CreateEntry(fileName);
            using StreamWriter zipEntryWriter = new StreamWriter(entry.Open());
            await fileStream.CopyToAsync(zipEntryWriter.BaseStream);
        }
    }
}

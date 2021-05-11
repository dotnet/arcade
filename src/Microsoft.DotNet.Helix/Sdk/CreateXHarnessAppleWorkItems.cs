using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided iOS app bundle paths.
    /// </summary>
    public class CreateXHarnessAppleWorkItems : XHarnessTaskBase
    {
        public const string TargetPropName = "Targets";
        public const string iOSTargetName = "ios-device";
        public const string tvOSTargetName = "tvos-device";

        private const string LaunchTimeoutPropName = "LaunchTimeout";
        private const string IncludesTestRunnerPropName = "IncludesTestRunner";

        private const string EntryPointScript = "xharness-helix-job.apple.sh";
        private const string RunnerScript = "xharness-runner.apple.sh";

        private static readonly TimeSpan s_defaultLaunchTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// An array of one or more paths to iOS/tvOS app bundles (folders ending with ".app" usually)
        /// that will be used to create Helix work items.
        /// </summary>
        [Required]
        public ITaskItem[] AppBundles { get; set; }

        /// <summary>
        /// Xcode version to use, e.g. 11.4 or 12.5_beta3.
        /// </summary>
        public string XcodeVersion { get; set; }

        /// <summary>
        /// URL template to get the provisioning profile that will be used to sign the app from (in case of real device targets).
        /// The URL is a template in the following format:
        /// https://storage.azure.com/signing/NET_Apple_Development_{PLATFORM}.mobileprovision
        /// </summary>
        public string ProvisioningProfileUrl { get; set; }

        /// <summary>
        /// Path where we can store intermediate files.
        /// </summary>
        public string TmpDir { get; set; }

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddProvisioningProfileProvider(ProvisioningProfileUrl, TmpDir);
            collection.TryAddTransient<IZipArchiveManager, ZipArchiveManager>();
            collection.TryAddTransient<IFileSystem, FileSystem>();
            collection.TryAddSingleton(Log);
        }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItems
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation</returns>
        public bool ExecuteTask(
            IProvisioningProfileProvider provisioningProfileProvider,
            IZipArchiveManager zipArchiveManager,
            IFileSystem fileSystem)
        {
            provisioningProfileProvider.AddProfilesToBundles(AppBundles);
            var tasks = AppBundles.Select(bundle => PrepareWorkItem(zipArchiveManager, fileSystem, bundle));

            WorkItems = Task.WhenAll(tasks).GetAwaiter().GetResult().Where(wi => wi != null).ToArray();
            
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Prepares HelixWorkItem that can run on an iOS device using XHarness
        /// </summary>
        /// <param name="appFolderPath">Path to application package</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(
            IZipArchiveManager zipArchiveManager,
            IFileSystem fileSystem,
            ITaskItem appBundleItem)
        {
            string appFolderPath = appBundleItem.ItemSpec.TrimEnd(Path.DirectorySeparatorChar);
            
            string workItemName = fileSystem.GetFileName(appFolderPath);
            if (workItemName.EndsWith(".app"))
            {
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            var (testTimeout, workItemTimeout, expectedExitCode) = ParseMetadata(appBundleItem);

            // Validation of any metadata specific to iOS stuff goes here
            if (!appBundleItem.TryGetMetadata(TargetPropName, out string target))
            {
                Log.LogError($"'{TargetPropName}' metadata must be specified - " +
                    "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                return null;
            }

            target = target.ToLowerInvariant();

            // Optional timeout for the how long it takes for the app to be installed, booted and tests start executing
            TimeSpan launchTimeout = s_defaultLaunchTimeout;
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
                Log.LogWarning("The ExpectedExitCode property is ignored in the `apple test` scenario");
            }

            string appName = fileSystem.GetFileName(appBundleItem.ItemSpec);
            string command = GetHelixCommand(appName, target, testTimeout, launchTimeout, includesTestRunner, expectedExitCode);
            string payloadArchivePath = await CreateZipArchiveOfFolder(zipArchiveManager, fileSystem, appFolderPath);

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appFolderPath}, Command: {command}");

            return new Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", payloadArchivePath },
                { "Command", command },
                { "Timeout", workItemTimeout.ToString() },
            });
        }

        private string GetHelixCommand(string appName, string targets, TimeSpan testTimeout, TimeSpan launchTimeout, bool includesTestRunner, int expectedExitCode) =>
            $"chmod +x {EntryPointScript} && ./{EntryPointScript} " +
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

        private async Task<string> CreateZipArchiveOfFolder(IZipArchiveManager zipArchiveManager, IFileSystem fileSystem, string folderToZip)
        {
            if (!fileSystem.DirectoryExists(folderToZip))
            {
                Log.LogError($"Cannot find path containing app: '{folderToZip}'");
                return string.Empty;
            }

            string appFolderDirectory = fileSystem.GetDirectoryName(folderToZip);
            string fileName = $"xharness-app-payload-{fileSystem.GetFileName(folderToZip).ToLowerInvariant()}.zip";
            string outputZipPath = fileSystem.PathCombine(appFolderDirectory, fileName);

            if (fileSystem.FileExists(outputZipPath))
            {
                Log.LogMessage($"Zip archive '{outputZipPath}' already exists, overwriting..");
                fileSystem.DeleteFile(outputZipPath);
            }

            zipArchiveManager.ArchiveDirectory(folderToZip, outputZipPath, true);

            Log.LogMessage($"Adding the XHarness job scripts into the payload archive");
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(outputZipPath, ScriptNamespace + EntryPointScript, EntryPointScript);
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(outputZipPath, ScriptNamespace + RunnerScript, RunnerScript);

            return outputZipPath;
        }
    }
}

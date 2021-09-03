using System;
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
        public const string iOSTargetName = "ios-device";
        public const string tvOSTargetName = "tvos-device";

        public static class MetadataNames
        {
            public const string Target = "TestTarget";
            public const string LaunchTimeout = "LaunchTimeout";
            public const string IncludesTestRunner = "IncludesTestRunner";
            public const string ResetSimulator = "ResetSimulator";
            public const string AppBundlePath = "AppBundlePath";
        }

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
        /// Indicates whether we should run a user supplied script to build the native app on the fly
        /// </summary>
        public bool ShouldBuildApps { get; set; }

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
            // The user can re-use the same .apk for 2 work items so the name of the work item will come from ItemSpec and path from metadata
            string workItemName;
            string appFolderPath;
            if (appBundleItem.TryGetMetadata(MetadataNames.AppBundlePath, out string appPathMetadata) && !string.IsNullOrEmpty(appPathMetadata))
            {
                workItemName = appBundleItem.ItemSpec;
                appFolderPath = appPathMetadata;
            }
            else
            {
                workItemName = fileSystem.GetFileName(appBundleItem.ItemSpec);
                appFolderPath = appBundleItem.ItemSpec;
            }

            appFolderPath = appFolderPath.TrimEnd(Path.DirectorySeparatorChar);

            bool isAlreadyArchived = workItemName.EndsWith(".zip");

            if (isAlreadyArchived)
            {
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            if (workItemName.EndsWith(".app"))
            {
                // If someone named the zip something.app.zip, we want both gone
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            if (!ValidateAppBundlePath(fileSystem, appFolderPath, isAlreadyArchived))
            {
                Log.LogError($"App bundle not found in {appFolderPath}");
                return null;
            }
            
            var (testTimeout, workItemTimeout, expectedExitCode, customCommands) = ParseMetadata(appBundleItem);

            // Validation of any metadata specific to iOS stuff goes here
            if (!appBundleItem.TryGetMetadata(MetadataNames.Target, out string target))
            {
                Log.LogError($"'{MetadataNames.Target}' metadata must be specified - " +
                    "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                return null;
            }

            target = target.ToLowerInvariant();

            // Optional timeout for the how long it takes for the app to be installed, booted and tests start executing
            TimeSpan launchTimeout = s_defaultLaunchTimeout;
            if (appBundleItem.TryGetMetadata(MetadataNames.LaunchTimeout, out string launchTimeoutProp))
            {
                if (!TimeSpan.TryParse(launchTimeoutProp, out launchTimeout) || launchTimeout.Ticks < 0)
                {
                    Log.LogError($"Invalid value \"{launchTimeoutProp}\" provided in <{MetadataNames.LaunchTimeout}>");
                    return null;
                }
            }

            bool includesTestRunner = true;
            if (appBundleItem.TryGetMetadata(MetadataNames.IncludesTestRunner, out string includesTestRunnerProp))
            {
                if (includesTestRunnerProp.ToLowerInvariant() == "false")
                {
                    includesTestRunner = false;
                }
            }

            if (includesTestRunner && expectedExitCode != 0 && customCommands != null)
            {
                Log.LogWarning("The ExpectedExitCode property is ignored in the `apple test` scenario");
            }

            bool resetSimulator = false;
            if (appBundleItem.TryGetMetadata(MetadataNames.ResetSimulator, out string resetSimulatorRunnerProp))
            {
                if (resetSimulatorRunnerProp.ToLowerInvariant() == "true")
                {
                    resetSimulator = true;
                }
            }

            if (customCommands == null)
            {
                // When no user commands are specified, we add the default `apple test ...` command
                customCommands = GetDefaultCommand(target, includesTestRunner, resetSimulator);
            }

            string appName = isAlreadyArchived ? $"{fileSystem.GetFileNameWithoutExtension(appFolderPath)}.app" : fileSystem.GetFileName(appFolderPath);
            string helixCommand = GetHelixCommand(appName, target, testTimeout, launchTimeout, includesTestRunner, expectedExitCode, resetSimulator);
            string payloadArchivePath = await CreateZipArchiveOfFolder(zipArchiveManager, fileSystem, workItemName, isAlreadyArchived, appFolderPath, customCommands);

            return CreateTaskItem(workItemName, payloadArchivePath, helixCommand, workItemTimeout);
        }

        private bool ValidateAppBundlePath(
            IFileSystem fileSystem, 
            string appBundlePath, 
            bool isAlreadyArchived)
        {
            return isAlreadyArchived ? fileSystem.FileExists(appBundlePath) : fileSystem.DirectoryExists(appBundlePath);
        }

        private string GetDefaultCommand(string target, bool includesTestRunner, bool resetSimulator) =>
            $"xharness apple {(includesTestRunner ? "test" : "run")} " +
            "--app \"$app\" " +
            "--output-directory \"$output_directory\" " +
            "--target \"$target\" " +
            "--timeout \"$timeout\" " +
            "--xcode \"$xcode_path\" " +
            "-v " +
            (includesTestRunner
                ? $"--launch-timeout \"$launch_timeout\" "
                : $"--expected-exit-code $expected_exit_code ") +
            (resetSimulator ? $"--reset-simulator " : string.Empty) +
            (target.Contains("device") ? $"--signal-app-end " : string.Empty) + // iOS/tvOS 14+ workaround
            (!string.IsNullOrEmpty(AppArguments) ? "-- " + AppArguments : string.Empty);

        private string GetHelixCommand(
            string appName,
            string target,
            TimeSpan testTimeout,
            TimeSpan launchTimeout,
            bool includesTestRunner,
            int expectedExitCode,
            bool resetSimulator)
            =>
            $"chmod +x {EntryPointScript} && ./{EntryPointScript} " +
            $"--app \"{appName}\" " +
            (ShouldBuildApps ? "--build " : string.Empty) +
            $"--target \"{target}\" " +
            $"--timeout \"{testTimeout}\" " +
            $"--launch-timeout \"{launchTimeout}\" " +
            (includesTestRunner ? "--includes-test-runner " : string.Empty) +
            (resetSimulator ? "--reset-simulator" : string.Empty) +
            $"--expected-exit-code \"{expectedExitCode}\" " +
            (!string.IsNullOrEmpty(XcodeVersion) ? $" --xcode-version \"{XcodeVersion}\"" : string.Empty) +
            (!string.IsNullOrEmpty(AppArguments) ? $" --app-arguments \"{AppArguments}\"" : string.Empty);

        private async Task<string> CreateZipArchiveOfFolder(
            IZipArchiveManager zipArchiveManager,
            IFileSystem fileSystem,
            string workItemName,
            bool isAlreadyArchived,
            string folderToZip,
            string injectedCommands)
        {
            string appFolderDirectory = fileSystem.GetDirectoryName(folderToZip);
            
            string fileName;
            string outputZipPath;

            if (!isAlreadyArchived)
            {
                fileName = $"xharness-app-payload-{workItemName.ToLowerInvariant()}.zip";
                outputZipPath = fileSystem.PathCombine(appFolderDirectory, fileName);

                if (fileSystem.FileExists(outputZipPath))
                {
                    Log.LogMessage($"Zip archive '{outputZipPath}' already exists, overwriting..");
                    fileSystem.DeleteFile(outputZipPath);
                }

                zipArchiveManager.ArchiveDirectory(folderToZip, outputZipPath, true);
            }
            else
            {
                Log.LogMessage($"App payload '{workItemName}` has already been zipped. Skipping creating zip archive");
                fileName = fileSystem.GetFileName(folderToZip);
                outputZipPath = fileSystem.PathCombine(appFolderDirectory, fileName);
            }

            Log.LogMessage($"Adding the XHarness job scripts into the payload archive '{ScriptNamespace}{EntryPointScript}'");
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(outputZipPath, ScriptNamespace + EntryPointScript, EntryPointScript);
            Log.LogMessage($"2nd AddResourceFileToArchive");
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(outputZipPath, ScriptNamespace + RunnerScript, RunnerScript);
            await zipArchiveManager.AddContentToArchive(outputZipPath, CustomCommandsScript + ".sh", injectedCommands);

            return outputZipPath;
        }
    }
}

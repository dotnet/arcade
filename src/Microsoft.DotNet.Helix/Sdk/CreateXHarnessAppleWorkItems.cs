using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided iOS app bundle paths.
    /// </summary>
    public class CreateXHarnessAppleWorkItems : XHarnessTaskBase
    {
        private const string EntryPointScriptName = "xharness-helix-job.apple.sh";
        private const string RunnerScriptName = "xharness-runner.apple.sh";
        private const string LaunchTimeoutPropName = "LaunchTimeout";
        private const string TargetPropName = "Targets";
        private const string IncludesTestRunnerPropName = "IncludesTestRunner";
        private const int DefaultLaunchTimeoutInMinutes = 10;

        private readonly IHelpers _helpers = new Arcade.Common.Helpers();

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
        /// URL template to get the provisioning profile that will be used to sign the app from (in case of real device targets).
        /// The URL is a template in the following format:
        /// https://storage.azure.com/signing/NET_Apple_Development_{PLATFORM}.mobileprovision
        /// </summary>
        public string ProvisioningProfileUrl { get; set; }

        /// <summary>
        /// Path where we can store intermediate files.
        /// </summary>
        public string TmpDir { get; set; }

        private enum TargetPlatform
        {
            iOS,
            tvOS,
        }

        private string GetProvisioningProfileFileName(TargetPlatform platform) => Path.GetFileName(GetProvisioningProfileUrl(platform));

        private string GetProvisioningProfileUrl(TargetPlatform platform) => ProvisioningProfileUrl.Replace("{PLATFORM}", platform.ToString());

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
            DownloadProvisioningProfiles();
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
            if (!appBundleItem.TryGetMetadata(TargetPropName, out string target))
            {
                Log.LogError("'Targets' metadata must be specified - " +
                    "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                return null;
            }

            target = target.ToLowerInvariant();

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
                Log.LogWarning("The ExpectedExitCode property is ignored in the `apple test` scenario");
            }

            bool isDeviceTarget = target.Contains("device");
            string provisioningProfileDest = Path.Combine(appFolderPath, "embedded.mobileprovision");

            // Handle files needed for signing
            if (isDeviceTarget)
            {
                if (string.IsNullOrEmpty(TmpDir))
                {
                    Log.LogError(nameof(TmpDir) + " parameter not set but required for real device targets!");
                    return null;
                }

                if (string.IsNullOrEmpty(ProvisioningProfileUrl) && !File.Exists(provisioningProfileDest))
                {
                    Log.LogError(nameof(ProvisioningProfileUrl) + " parameter not set but required for real device targets!");
                    return null;
                }

                if (!File.Exists(provisioningProfileDest))
                {
                    // StartsWith because suffix can be the target OS version
                    TargetPlatform? platform = null;
                    if (target.StartsWith("ios-device"))
                    {
                        platform = TargetPlatform.iOS;
                    }
                    else if (target.StartsWith("tvos-device"))
                    {
                        platform = TargetPlatform.tvOS;
                    }

                    if (platform.HasValue)
                    {
                        string profilePath = Path.Combine(TmpDir, GetProvisioningProfileFileName(platform.Value));
                        Log.LogMessage($"Adding provisioning profile `{profilePath}` into the app bundle at `{provisioningProfileDest}`");
                        File.Copy(profilePath, provisioningProfileDest);
                    }
                }
                else
                {
                    Log.LogMessage($"Bundle already contains a provisioning profile at `{provisioningProfileDest}`");
                }
            }

            string appName = Path.GetFileName(appBundleItem.ItemSpec);
            string command = GetHelixCommand(appName, target, testTimeout, launchTimeout, includesTestRunner, expectedExitCode);
            string payloadArchivePath = await CreateZipArchiveOfFolder(appFolderPath);

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
            await AddResourceFileToPayload(outputZipPath, EntryPointScriptName);
            await AddResourceFileToPayload(outputZipPath, RunnerScriptName);

            return outputZipPath;
        }

        private void DownloadProvisioningProfiles()
        {
            if (string.IsNullOrEmpty(ProvisioningProfileUrl))
            {
                return;
            }

            string[] targets = AppBundles
                .Select(appBundle => appBundle.TryGetMetadata(TargetPropName, out string target) ? target : null)
                .Where(t => t != null)
                .ToArray();

            bool hasiOSTargets = targets.Contains("ios-device");
            bool hastvOSTargets = targets.Contains("tvos-device");

            if (hasiOSTargets)
            {
                DownloadProvisioningProfile(TargetPlatform.iOS);
            }

            if (hastvOSTargets)
            {
                DownloadProvisioningProfile(TargetPlatform.tvOS);
            }
        }

        private void DownloadProvisioningProfile(TargetPlatform platform)
        {
            var targetFile = Path.Combine(TmpDir, GetProvisioningProfileFileName(platform));

            using var client = new WebClient();
            _helpers.DirectoryMutexExec(async () => {
                if (File.Exists(targetFile))
                {
                    Log.LogMessage($"Provisioning profile is already downloaded");
                    return;
                }

                Log.LogMessage($"Downloading {platform} provisioning profile to {targetFile}");

                await client.DownloadFileTaskAsync(new Uri(GetProvisioningProfileUrl(platform)), targetFile);
            }, TmpDir);
        }
    }
}

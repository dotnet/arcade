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
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public class CreateXHarnessAndroidWorkItems : XHarnessTaskBase
    {
        private const string PosixAndroidWrapperScript = "xharness-helix-job.android.sh";
        private const string NonPosixAndroidWrapperScript = "xharness-helix-job.android.bat";

        /// <summary>
        /// An array of one or more paths to application packages (.apk for Android)
        /// that will be used to create Helix work items.
        /// </summary>
        public ITaskItem[] Apks { get; set; }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItems
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation</returns>
        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Create work items for XHarness test execution
        /// </summary>
        /// <returns></returns>
        private async Task ExecuteAsync()
        {
            WorkItems = (await Task.WhenAll(Apks.Select(PrepareWorkItem))).Where(wi => wi != null).ToArray();
        }

        /// <summary>
        /// Prepares HelixWorkItem that can run on a device (currently Android or iOS) using XHarness
        /// </summary>
        /// <param name="appPackage">Path to application package</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(ITaskItem appPackage)
        {
            // Forces this task to run asynchronously
            await Task.Yield();
            string workItemName = Path.GetFileNameWithoutExtension(appPackage.ItemSpec);

            var (testTimeout, workItemTimeout, expectedExitCode) = ParseMetadata(appPackage);

            string command = ValidateMetadataAndGetXHarnessAndroidCommand(appPackage, testTimeout, expectedExitCode);

            if (!Path.GetExtension(appPackage.ItemSpec).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError($"Unsupported app package type: {Path.GetFileName(appPackage.ItemSpec)}");
                return null;
            }

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appPackage.ItemSpec}, Command: {command}");

            string workItemZip = await CreateZipArchiveOfPackageAsync(appPackage.ItemSpec);

            return new Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", workItemZip},
                { "Command", command },
                { "Timeout", workItemTimeout.ToString() },
            });
        }

        private async Task<string> CreateZipArchiveOfPackageAsync(string fileToZip)
        {
            string directoryOfPackage = Path.GetDirectoryName(fileToZip);
            string fileName = $"xharness-apk-payload-{Path.GetFileNameWithoutExtension(fileToZip).ToLowerInvariant()}.zip";
            string outputZipAbsolutePath = Path.Combine(directoryOfPackage, fileName);
            using (FileStream fs = File.OpenWrite(outputZipAbsolutePath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                zip.CreateEntryFromFile(fileToZip, Path.GetFileName(fileToZip));
            }

            // WorkItem payloads of APKs can be reused if sent to multiple queues at once,
            // so we'll always include both scripts (very small)
            await AddResourceFileToPayload(outputZipAbsolutePath, PosixAndroidWrapperScript);
            await AddResourceFileToPayload(outputZipAbsolutePath, NonPosixAndroidWrapperScript);

            return outputZipAbsolutePath;
        }

        private string ValidateMetadataAndGetXHarnessAndroidCommand(ITaskItem appPackage, TimeSpan xHarnessTimeout, int expectedExitCode)
        {
            // Validation of any metadata specific to Android stuff goes here
            if (!appPackage.GetRequiredMetadata(Log, "AndroidPackageName", out string androidPackageName))
            {
                Log.LogError("AndroidPackageName metadata must be specified; this may match, but can vary from file name");
                return null;
            }

            appPackage.TryGetMetadata("Arguments", out string arguments);
            appPackage.TryGetMetadata("AndroidInstrumentationName", out string androidInstrumentationName);
            appPackage.TryGetMetadata("DeviceOutputPath", out string deviceOutputPath);

            string outputPathArg = string.IsNullOrEmpty(deviceOutputPath) ? string.Empty : $"--dev-out={deviceOutputPath} ";
            string instrumentationArg = string.IsNullOrEmpty(androidInstrumentationName) ? string.Empty : $"-i={androidInstrumentationName} ";

            string outputDirectory = IsPosixShell ? "$HELIX_WORKITEM_UPLOAD_ROOT" : "%HELIX_WORKITEM_UPLOAD_ROOT%";
            string wrapperScriptName = IsPosixShell ? PosixAndroidWrapperScript : NonPosixAndroidWrapperScript;

            string xharnessHelixWrapperScript = IsPosixShell ? $"chmod +x ./{wrapperScriptName} && ./{wrapperScriptName}"
                                                             : $"{wrapperScriptName}";

            string xharnessRunCommand = $"{xharnessHelixWrapperScript} " +
                                        $"dotnet exec \"{(IsPosixShell ? "$XHARNESS_CLI_PATH" : "%XHARNESS_CLI_PATH%")}\" android test " +
                                        $"--app \"{Path.GetFileName(appPackage.ItemSpec)}\" " +
                                        $"--output-directory \"{outputDirectory}\" " +
                                        $"--timeout \"{xHarnessTimeout}\" " +
                                        $"-p=\"{androidPackageName}\" " +
                                        "-v " +
                                        (expectedExitCode != 0 ? $" --expected-exit-code \"{expectedExitCode}\" " : string.Empty) +
                                        outputPathArg +
                                        instrumentationArg +
                                        arguments +
                                        (!string.IsNullOrEmpty(AppArguments) ? $" -- {AppArguments}" : string.Empty);

            Log.LogMessage(MessageImportance.Low, $"Generated XHarness command: {xharnessRunCommand}");

            return xharnessRunCommand;
        }
    }
}

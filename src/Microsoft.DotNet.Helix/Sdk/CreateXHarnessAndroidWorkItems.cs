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
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public class CreateXHarnessAndroidWorkItems : XHarnessTaskBase
    {
        private const string ArgumentsPropName = "Arguments";
        private const string AndroidInstrumentationNamePropName = "AndroidInstrumentationName";
        private const string DeviceOutputPathPropName = "DeviceOutputPath";

        private const string PosixAndroidWrapperScript = "tools.xharness_runner.xharness-helix-job.android.sh";
        private const string NonPosixAndroidWrapperScript = "tools.xharness_runner.xharness-helix-job.android.bat";

        /// <summary>
        /// An array of one or more paths to application packages (.apk for Android)
        /// that will be used to create Helix work items.
        /// </summary>
        public ITaskItem[] Apks { get; set; }

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddTransient<IZipArchiveManager, ZipArchiveManager>();
            collection.TryAddSingleton(Log);
        }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItems
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation</returns>
        public bool Execute(IZipArchiveManager zipArchiveManager)
        {
            var tasks = Apks.Select(apk => PrepareWorkItem(zipArchiveManager, apk));

            WorkItems = Task.WhenAll(tasks).GetAwaiter().GetResult().Where(wi => wi != null).ToArray();

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Prepares HelixWorkItem that can run on a device (currently Android or iOS) using XHarness
        /// </summary>
        /// <param name="appPackage">Path to application package</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(IZipArchiveManager zipArchiveManager, ITaskItem appPackage)
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

            string workItemZip = await CreateZipArchiveOfPackageAsync(zipArchiveManager, appPackage.ItemSpec);

            return new Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", workItemZip },
                { "Command", command },
                { "Timeout", workItemTimeout.ToString() },
            });
        }

        private async Task<string> CreateZipArchiveOfPackageAsync(IZipArchiveManager zipArchiveManager, string fileToZip)
        {
            string fileName = $"xharness-apk-payload-{Path.GetFileNameWithoutExtension(fileToZip).ToLowerInvariant()}.zip";
            string outputZipPath = Path.Combine(Path.GetDirectoryName(fileToZip), fileName);

            if (File.Exists(outputZipPath))
            {
                Log.LogMessage($"Zip archive '{outputZipPath}' already exists, overwriting..");
                File.Delete(outputZipPath);
            }

            zipArchiveManager.ArchiveFile(fileToZip, outputZipPath);

            // WorkItem payloads of APKs can be reused if sent to multiple queues at once,
            // so we'll always include both scripts (very small)
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAndroidWorkItems>(outputZipPath, PosixAndroidWrapperScript);
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAndroidWorkItems>(outputZipPath, NonPosixAndroidWrapperScript);

            return outputZipPath;
        }

        private string ValidateMetadataAndGetXHarnessAndroidCommand(ITaskItem appPackage, TimeSpan xHarnessTimeout, int expectedExitCode)
        {
            // Validation of any metadata specific to Android stuff goes here
            if (!appPackage.GetRequiredMetadata(Log, "AndroidPackageName", out string androidPackageName))
            {
                Log.LogError("AndroidPackageName metadata must be specified; this may match, but can vary from file name");
                return null;
            }

            appPackage.TryGetMetadata(ArgumentsPropName, out string arguments);
            appPackage.TryGetMetadata(AndroidInstrumentationNamePropName, out string androidInstrumentationName);
            appPackage.TryGetMetadata(DeviceOutputPathPropName, out string deviceOutputPath);

            string outputPathArg = string.IsNullOrEmpty(deviceOutputPath) ? string.Empty : $"--dev-out={deviceOutputPath} ";
            string instrumentationArg = string.IsNullOrEmpty(androidInstrumentationName) ? string.Empty : $"-i={androidInstrumentationName} ";

            string outputDirectory = IsPosixShell ? "$HELIX_WORKITEM_UPLOAD_ROOT" : "%HELIX_WORKITEM_UPLOAD_ROOT%";
            string wrapperScriptName = IsPosixShell ? PosixAndroidWrapperScript : NonPosixAndroidWrapperScript;

            string xharnessHelixWrapperScript = IsPosixShell ? $"chmod +x ./{wrapperScriptName} && ./{wrapperScriptName}"
                                                             : $"call {wrapperScriptName}";

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

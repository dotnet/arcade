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
        private const string AndroidPackageNamePropName = "AndroidPackageName";

        private const string PosixAndroidWrapperScript = "xharness-helix-job.android.sh";
        private const string NonPosixAndroidWrapperScript = "xharness-helix-job.android.bat";

        /// <summary>
        /// Boolean true if this is a posix shell, false if not.
        /// This does not need to be set by a user; it is automatically determined in Microsoft.DotNet.Helix.Sdk.MonoQueue.targets
        /// </summary>
        [Required]
        public bool IsPosixShell { get; set; }

        /// <summary>
        /// An array of one or more paths to application packages (.apk for Android)
        /// that will be used to create Helix work items.
        /// </summary>
        public ITaskItem[] Apks { get; set; }

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddTransient<IZipArchiveManager, ZipArchiveManager>();
            collection.TryAddTransient<IFileSystem, FileSystem>();
            collection.TryAddSingleton(Log);
        }

        /// <summary>
        /// The main method of this MSBuild task which calls the asynchronous execution method and
        /// collates logged errors in order to determine the success of HelixWorkItems
        /// </summary>
        /// <returns>A boolean value indicating the success of HelixWorkItem creation</returns>
        public bool ExecuteTask(IZipArchiveManager zipArchiveManager, IFileSystem fileSystem)
        {
            var tasks = Apks.Select(apk => PrepareWorkItem(zipArchiveManager, fileSystem, apk));

            WorkItems = Task.WhenAll(tasks).GetAwaiter().GetResult().Where(wi => wi != null).ToArray();

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Prepares HelixWorkItem that can run on a device (currently Android or iOS) using XHarness
        /// </summary>
        /// <param name="appPackage">Path to application package</param>
        /// <returns>An ITaskItem instance representing the prepared HelixWorkItem.</returns>
        private async Task<ITaskItem> PrepareWorkItem(IZipArchiveManager zipArchiveManager, IFileSystem fileSystem, ITaskItem appPackage)
        {
            string workItemName = fileSystem.GetFileNameWithoutExtension(appPackage.ItemSpec);

            var (testTimeout, workItemTimeout, expectedExitCode) = ParseMetadata(appPackage);

            string command = ValidateMetadataAndGetXHarnessAndroidCommand(appPackage, testTimeout, expectedExitCode);

            if (!fileSystem.GetExtension(appPackage.ItemSpec).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError($"Unsupported app package type: {fileSystem.GetFileName(appPackage.ItemSpec)}");
                return null;
            }

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appPackage.ItemSpec}, Command: {command}");

            string workItemZip = await CreateZipArchiveOfPackageAsync(zipArchiveManager, fileSystem, appPackage.ItemSpec);

            return new Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", workItemZip },
                { "Command", command },
                { "Timeout", workItemTimeout.ToString() },
            });
        }

        private async Task<string> CreateZipArchiveOfPackageAsync(IZipArchiveManager zipArchiveManager, IFileSystem fileSystem, string fileToZip)
        {
            string fileName = $"xharness-apk-payload-{fileSystem.GetFileNameWithoutExtension(fileToZip).ToLowerInvariant()}.zip";
            string outputZipPath = fileSystem.PathCombine(fileSystem.GetDirectoryName(fileToZip), fileName);

            if (fileSystem.FileExists(outputZipPath))
            {
                Log.LogMessage($"Zip archive '{outputZipPath}' already exists, overwriting..");
                fileSystem.DeleteFile(outputZipPath);
            }

            zipArchiveManager.ArchiveFile(fileToZip, outputZipPath);

            // WorkItem payloads of APKs can be reused if sent to multiple queues at once,
            // so we'll always include both scripts (very small)
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAndroidWorkItems>(outputZipPath, ScriptNamespace + PosixAndroidWrapperScript, PosixAndroidWrapperScript);
            await zipArchiveManager.AddResourceFileToArchive<CreateXHarnessAndroidWorkItems>(outputZipPath, ScriptNamespace + NonPosixAndroidWrapperScript, NonPosixAndroidWrapperScript);

            return outputZipPath;
        }

        private string ValidateMetadataAndGetXHarnessAndroidCommand(ITaskItem appPackage, TimeSpan xHarnessTimeout, int expectedExitCode)
        {
            // Validation of any metadata specific to Android stuff goes here
            if (!appPackage.GetRequiredMetadata(Log, AndroidPackageNamePropName, out string androidPackageName))
            {
                Log.LogError($"{AndroidPackageNamePropName} metadata must be specified; this may match, but can vary from file name");
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

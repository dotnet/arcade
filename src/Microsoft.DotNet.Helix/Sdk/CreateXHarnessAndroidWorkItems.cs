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
    /// MSBuild custom task to create HelixWorkItems for provided Android application packages.
    /// </summary>
    public class CreateXHarnessAndroidWorkItems : XHarnessTaskBase
    {
        public static class MetadataNames
        {
            public const string Arguments = "Arguments";
            public const string AndroidInstrumentationName = "AndroidInstrumentationName";
            public const string DeviceOutputPath = "DeviceOutputPath";
            public const string AndroidPackageName = "AndroidPackageName";
        }

        private const string PosixAndroidWrapperScript = "xharness-helix-job.android.sh";
        private const string NonPosixAndroidWrapperScript = "xharness-helix-job.android.ps1";

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

            var (testTimeout, workItemTimeout, expectedExitCode, customCommands) = ParseMetadata(appPackage);

            if (!fileSystem.GetExtension(appPackage.ItemSpec).Equals(".apk", StringComparison.OrdinalIgnoreCase))
            {
                Log.LogError($"Unsupported app package type: {fileSystem.GetFileName(appPackage.ItemSpec)}");
                return null;
            }

            // Validation of any metadata specific to Android stuff goes here
            if (!appPackage.GetRequiredMetadata(Log, MetadataNames.AndroidPackageName, out string androidPackageName))
            {
                Log.LogError($"{MetadataNames.AndroidPackageName} metadata must be specified; this may match, but can vary from file name");
                return null;
            }

            appPackage.TryGetMetadata(MetadataNames.AndroidInstrumentationName, out string androidInstrumentationName);

            string command = GetHelixCommand(appPackage, androidPackageName, testTimeout, expectedExitCode);

            appPackage.TryGetMetadata(MetadataNames.Arguments, out string extraArguments);

            if (customCommands == null)
            {
                var exitCodeArg = expectedExitCode != 0 ? $" --expected-exit-code $expected_exit_code " : string.Empty;
                var passthroughArgs = !string.IsNullOrEmpty(AppArguments) ? $" -- {AppArguments}" : string.Empty;
                var instrumentationArg = !string.IsNullOrEmpty(androidInstrumentationName) ? $" --instrumentation \"$instrumentation\" " : string.Empty;

                // In case user didn't specify custom commands, we use our default one
                customCommands = "xharness android test " +
                    "--app \"$app\" " +
                    "--output - directory \"$output_directory\" " +
                    "--timeout \"$timeout\" " +
                    "--package-name \"$package_name\" " +
                    " -v " +
                    " --dev-out \"$device_output_path\" "
                    + instrumentationArg
                    + exitCodeArg
                    + extraArguments
                    + passthroughArgs;
            }

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appPackage.ItemSpec}, Command: {command}");

            string workItemZip = await CreateZipArchiveOfPackageAsync(zipArchiveManager, fileSystem, appPackage.ItemSpec, customCommands);

            return CreateTaskItem(workItemName, workItemZip, command, workItemTimeout);
        }

        private string GetHelixCommand(ITaskItem appPackage, string androidPackageName, TimeSpan xHarnessTimeout, int expectedExitCode)
        {
            appPackage.TryGetMetadata(MetadataNames.AndroidInstrumentationName, out string androidInstrumentationName);
            appPackage.TryGetMetadata(MetadataNames.DeviceOutputPath, out string deviceOutputPath);

            string wrapperScriptName = IsPosixShell ? PosixAndroidWrapperScript : NonPosixAndroidWrapperScript;
            string xharnessHelixWrapperScript = IsPosixShell ? $"chmod +x ./{wrapperScriptName} && ./{wrapperScriptName}"
                                                             : $"powershell -ExecutionPolicy ByPass -NoProfile -File \"{wrapperScriptName}\"";

            // We either call .ps1 or .sh so we need to format the arguments well (PS has -argument, bash has --argument)
            string dash = IsPosixShell ? "--" : "-";
            string xharnessRunCommand = $"{xharnessHelixWrapperScript} " +
                $"{dash}app \"{Path.GetFileName(appPackage.ItemSpec)}\" " +
                $"{dash}timeout \"{xHarnessTimeout}\" " +
                $"{dash}package_name \"{androidPackageName}\" " +
                (expectedExitCode != 0 ? $" {dash}expected_exit_code \"{expectedExitCode}\" " : string.Empty) +
                (string.IsNullOrEmpty(deviceOutputPath) ? string.Empty : $"{dash}device_output_path \"{deviceOutputPath}\" ") +
                (string.IsNullOrEmpty(androidInstrumentationName) ? string.Empty : $"{dash}instrumentation \"{androidInstrumentationName}\" ");

            Log.LogMessage(MessageImportance.Low, $"Generated XHarness command: {xharnessRunCommand}");

            return xharnessRunCommand;
        }

        private async Task<string> CreateZipArchiveOfPackageAsync(
            IZipArchiveManager zipArchiveManager,
            IFileSystem fileSystem,
            string fileToZip,
            string injectedCommands)
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
            await zipArchiveManager.AddContentToArchive(outputZipPath, CustomCommandsScript + (IsPosixShell ? ".sh" : ".ps1"), injectedCommands);

            return outputZipPath;
        }
    }
}

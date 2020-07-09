using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    /// <summary>
    /// MSBuild custom task to create HelixWorkItems for provided iOS app bundle paths.
    /// </summary>
    public class CreateXHarnessiOSWorkItems : XHarnessTaskBase
    {
        private const string PayloadScriptName = "ios-helix-job-payload.sh";

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

            if (string.IsNullOrEmpty(XcodeVersion))
            {
                Log.LogError("No Xcode version was specified.");
                return false;
            }

            if (!Regex.IsMatch(XcodeVersion, "[0-9]+\\.[0-9]+"))
            {
                Log.LogError($"Xcode version '{XcodeVersion}' was in an invalid format. Expected format is [major].[minor] format, e.g. 11.4.");
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
        private async Task<ITaskItem> PrepareWorkItem(ITaskItem appFolderItem)
        {
            // Forces this task to run asynchronously
            await Task.Yield();

            string appFolderPath = appFolderItem.ItemSpec.TrimEnd(Path.DirectorySeparatorChar);
            
            string workItemName = Path.GetFileName(appFolderPath);
            if (workItemName.EndsWith(".app"))
            {
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            var timeouts = ParseTimeouts();

            string command = ValidateMetadataAndGetXHarnessiOSCommand(appFolderItem, timeouts.TestTimeout);

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appFolderPath}, Command: {command}");

            return new Microsoft.Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", await CreateZipArchiveOfFolder(appFolderPath) },
                { "Command", command },
                { "Timeout", timeouts.WorkItemTimeout.ToString() },
            });
        }

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

            // Add the payload script
            Log.LogMessage($"Adding the Helix job payload script into the ziparchive");

            using FileStream zipToOpen = new FileStream(outputZipPath, FileMode.Open);
            using ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.CreateEntry(PayloadScriptName);
            using StreamWriter zipEntryWriter = new StreamWriter(entry.Open());
            using FileStream payloadScriptStream = GetPayloadScriptStream();
            await payloadScriptStream.CopyToAsync(zipEntryWriter.BaseStream);

            return outputZipPath;
        }

        private string ValidateMetadataAndGetXHarnessiOSCommand(ITaskItem appFolderPath, TimeSpan xHarnessTimeout)
        {
            // Validation of any metadata specific to iOS stuff goes here
            if (!appFolderPath.GetRequiredMetadata(Log, "Targets", out string targets))
            {
                Log.LogError("'Targets' metadata must be specified - " +
                    "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                return null;
            }

            // We need to call 'sudo launchctl' to spawn the process in a user session with GUI rendering capabilities
            string xharnessRunCommand = $"sudo launchctl asuser `id -u` sh \"{PayloadScriptName}\" " +
                                        $"--app \"$HELIX_WORKITEM_ROOT/{Path.GetFileName(appFolderPath.ItemSpec)}\" " +
                                         "--output-directory \"$HELIX_WORKITEM_UPLOAD_ROOT\" " +
                                        $"--targets \"{targets}\" " +
                                        $"--timeout \"{xHarnessTimeout.TotalSeconds}\" " +
                                         "--launch-timeout 900 " +
                                         "--xharness-cli-path \"$XHARNESS_CLI_PATH\" " +
                                        $"--xcode-version {XcodeVersion}" +
                                        (!string.IsNullOrEmpty(AppArguments) ? $" --app-arguments \"{AppArguments}\"" : string.Empty);

            Log.LogMessage(MessageImportance.Low, $"Generated XHarness command: {xharnessRunCommand}");

            return xharnessRunCommand;
        }

        private static FileStream GetPayloadScriptStream()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var scriptPath = Path.Combine(assemblyDirectory, "tools", "xharness-runner", PayloadScriptName);
            return File.OpenRead(scriptPath);
        }
    }
}

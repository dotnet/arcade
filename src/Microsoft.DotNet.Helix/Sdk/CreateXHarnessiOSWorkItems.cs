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
    /// MSBuild custom task to create HelixWorkItems for provided iOS application folder paths.
    /// </summary>
    public class CreateXHarnessiOSWorkItems : XHarnessTaskBase
    {
        /// <summary>
        /// An array of one or more paths to application packages (.apk for Android)
        /// that will be used to create Helix work items.
        /// </summary>
        public ITaskItem[] AppFolders { get; set; }

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
            WorkItems = (await Task.WhenAll(AppFolders.Select(PrepareWorkItem))).Where(wi => wi != null).ToArray();
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
            
            string appFolderPath = appFolderItem.ItemSpec;
            
            string workItemName = $"xharness-{Path.GetFileName(appFolderPath)}";
            if (workItemName.EndsWith(".app"))
            {
                workItemName = workItemName.Substring(0, workItemName.Length - 4);
            }

            TimeSpan timeout = ParseTimeout();

            string command = ValidateMetadataAndGetXHarnessiOSCommand(appFolderItem, timeout);

            Log.LogMessage($"Creating work item with properties Identity: {workItemName}, Payload: {appFolderPath}, Command: {command}");

            return new Microsoft.Build.Utilities.TaskItem(workItemName, new Dictionary<string, string>()
            {
                { "Identity", workItemName },
                { "PayloadArchive", CreateZipArchiveOfFolder(appFolderPath, "./") },
                { "Command", command },
                { "Timeout", timeout.ToString() },
            });
        }

        private string CreateZipArchiveOfFolder(string folderToZip, string outputFolder)
        {
            if (!Directory.Exists(folderToZip))
            {
                Log.LogError($"Cannot find path containing app: {folderToZip}");
                return string.Empty;
            }

            string fileName = $"xharness-ios-app-payload-{Path.GetFileName(folderToZip).ToLowerInvariant()}.zip";
            string outputZipAbsolutePath = Path.Combine(outputFolder, fileName);
            ZipFile.CreateFromDirectory(folderToZip, outputZipAbsolutePath);
            return outputZipAbsolutePath;
        }

        private string ValidateMetadataAndGetXHarnessiOSCommand(ITaskItem appFolderPath, TimeSpan xHarnessTimeout)
        {
            // Validation of any metadata specific to iOS stuff goes here
            if (!appFolderPath.GetRequiredMetadata(Log, "Targets", out string targets))
            {
                Log.LogError("'Targets' metadata must be specified; this may match, but can vary from file name");
                return null;
            }

            string xharnessRunCommand = $"xharness ios test " +
                                        $"--app $HELIX_WORKITEM_ROOT/{Path.GetFileName(appFolderPath.ItemSpec)} " +
                                        $"--output-directory=$HELIX_WORKITEM_UPLOAD_ROOT " +
                                        $"--targets={targets} " +
                                        $"--timeout={xHarnessTimeout.TotalSeconds} " +
                                        $"-v";

            Log.LogMessage(MessageImportance.Low, $"Generated XHarness command: {xharnessRunCommand}");

            return xharnessRunCommand;
        }
    }
}

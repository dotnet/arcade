using Microsoft.Build.Framework;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class DownloadFromResultsContainer : BaseTask
    {
        [Required]
        public ITaskItem[] WorkItems { get; set; }

        [Required]
        public string ResultsContainer { get; set; }

        [Required]
        public string PathToDownload { get; set; }

        [Required]
        public string JobId { get; set; }

        [Required]
        public ITaskItem[] MetadataToWrite { get; set; }

        private const string MetadataFile = "metadata.txt";

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(ResultsContainer))
            {
                LogRequiredParameterError(nameof(ResultsContainer));
            }

            if (string.IsNullOrEmpty(PathToDownload))
            {
                LogRequiredParameterError(nameof(PathToDownload));
            }

            if (string.IsNullOrEmpty(JobId))
            {
                LogRequiredParameterError(nameof(JobId));
            }

            if (!Log.HasLoggedErrors)
            {
                Log.LogMessage(MessageImportance.High, $"Downloading result files for job {JobId}");
                ExecuteCore().GetAwaiter().GetResult();
            }

            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteCore()
        {
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(PathToDownload, JobId));
            using (FileStream stream = File.Open(Path.Combine(directory.FullName, MetadataFile), FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                foreach (ITaskItem metadata in MetadataToWrite)
                {
                    await writer.WriteLineAsync(metadata.GetMetadata("Identity"));
                }
            }

            ResultsContainer = ResultsContainer.EndsWith("/") ? ResultsContainer : ResultsContainer + "/";
            await Task.WhenAll(WorkItems.Select(wi => DownloadFilesForWorkItem(wi, directory.FullName)));
            return;
        }

        private async Task DownloadFilesForWorkItem(ITaskItem workItem, string directoryPath)
        {
            if (workItem.GetRequiredMetadata(Log, "DownloadFilesFromResults", out string files))
            {
                string workItemName = workItem.GetMetadata("Identity");
                string[] filesToDownload = files.Split(';');

                DirectoryInfo destinationDir = Directory.CreateDirectory(Path.Combine(directoryPath, workItemName));
                foreach (var file in filesToDownload)
                {
                    try
                    {
                        string destinationFile = Path.Combine(destinationDir.FullName, file);
                        Log.LogMessage(MessageImportance.Normal, $"Downloading {file} => {destinationFile}...");
                        CloudBlob blob = new CloudBlob(new Uri($"{ResultsContainer}{workItemName}/{file}"));
                        await blob.DownloadToFileAsync(destinationFile, FileMode.Create);
                    }
                    catch (StorageException e)
                    {
                        Log.LogWarning($"Failed to download {file} from results container: {e.Message}");
                    }
                }
            };
            return;
        }

        private void LogRequiredParameterError(string parameter)
        {
            Log.LogError($"Required parameter {nameof(parameter)} string was null or empty");
        }
    }
}

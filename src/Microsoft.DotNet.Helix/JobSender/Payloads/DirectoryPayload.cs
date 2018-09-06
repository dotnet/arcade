using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class DirectoryPayload : IPayload
    {
        public DirectoryPayload(string directory)
        {
            DirectoryInfo = new DirectoryInfo(directory);
            if (!DirectoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"The directory '{directory}' was not found.");
            }
        }

        public DirectoryInfo DirectoryInfo { get; }

        public async Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    string basePath = DirectoryInfo.FullName;

                    foreach (FileInfo file in DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.FullName.Substring(basePath.Length+1); // +1 prevents it from including the leading backslash
                        zip.CreateEntryFromFile(file.FullName, relativePath);
                    }
                }
                stream.Position = 0;
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{DirectoryInfo.Name}.zip");
                return zipUri.ToString();
            }
        }
    }
}

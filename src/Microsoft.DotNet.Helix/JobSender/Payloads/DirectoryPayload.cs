using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class DirectoryPayload : IPayload
    {
        public DirectoryPayload(string directory, bool includeDirectoryName)
        {
            IncludeDirectoryName = includeDirectoryName;
            DirectoryInfo = new DirectoryInfo(directory);
            if (!DirectoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"The directory '{directory}' was not found.");
            }
        }

        public DirectoryInfo DirectoryInfo { get; }

        public bool IncludeDirectoryName { get; }

        public async Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    string basePath = DirectoryInfo.FullName;
                    basePath = basePath.TrimEnd('/', '\\');

                    foreach (FileInfo file in DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.FullName.Substring(basePath.Length + 1); // +1 prevents it from including the leading backslash
                        string zipEntryName = relativePath.Replace('\\', '/'); // Normalize slashes

                        if (IncludeDirectoryName)
                        {
                            zipEntryName = DirectoryInfo.Name + "/" + zipEntryName;
                        }

                        zip.CreateEntryFromFile(file.FullName, zipEntryName);
                    }
                }
                stream.Position = 0;
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{Guid.NewGuid()}.zip");
                return zipUri.AbsoluteUri;
            }
        }
    }
}

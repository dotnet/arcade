using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class DirectoryPayload : IPayload
    {
        public DirectoryPayload(string directory, string archiveEntryPrefix)
        {
            ArchiveEntryPrefix = archiveEntryPrefix;
            DirectoryInfo = new DirectoryInfo(directory);
            if (!DirectoryInfo.Exists)
            {
                throw new DirectoryNotFoundException($"The directory '{directory}' was not found.");
            }
        }

        public DirectoryInfo DirectoryInfo { get; }

        public string ArchiveEntryPrefix { get; }

#pragma warning disable 1998
        public async Task<string> UploadAsync(IBlobContainer payloadContainer)
#pragma warning restore 1998
        {
            string dirHash;
            using (var hasher = SHA256.Create())
            {
                dirHash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(DirectoryInfo.FullName)));
            }
            using (var mutex = new Mutex(false, $"Global\\{dirHash}"))
            {
                bool hasMutex = false;
                try
                {
                    try
                    {
                        mutex.WaitOne();
                    }
                    catch (AbandonedMutexException)
                    {
                    }

                    hasMutex = true;
                    return DoUploadAsync(payloadContainer).GetAwaiter().GetResult(); // Can't await because of mutex
                }
                finally
                {
                    if (hasMutex)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        private async Task<string> DoUploadAsync(IBlobContainer payloadContainer)
        {
            var alreadyUploadedFile = new FileInfo(Helpers.RemoveTrailingSlash(DirectoryInfo.FullName) + ".payload");
            if (alreadyUploadedFile.Exists && IsUpToDate(alreadyUploadedFile))
            {
                Console.WriteLine($"Using previously uploaded payload for {DirectoryInfo.FullName}");
                return File.ReadAllText(alreadyUploadedFile.FullName);
            }

            Console.WriteLine($"Uploading payload for {DirectoryInfo.FullName}");
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    string basePath = DirectoryInfo.FullName;
                    basePath = basePath.TrimEnd('/', '\\');

                    foreach (FileInfo file in DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        string relativePath =
                            file.FullName.Substring(basePath.Length + 1); // +1 prevents it from including the leading backslash
                        string zipEntryName = relativePath.Replace('\\', '/'); // Normalize slashes

                        if (!string.IsNullOrEmpty(ArchiveEntryPrefix))
                        {
                            zipEntryName = ArchiveEntryPrefix + "/" + zipEntryName;
                        }

                        zip.CreateEntryFromFile(file.FullName, zipEntryName);
                    }
                }

                stream.Position = 0;
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{Guid.NewGuid()}.zip");
                File.WriteAllText(alreadyUploadedFile.FullName, zipUri.AbsoluteUri);
                return zipUri.AbsoluteUri;
            }
        }

        private bool IsUpToDate(FileInfo alreadyUploadedFile)
        {
            var newestFileWriteTime = DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .Select(file => file.LastWriteTimeUtc)
                .Max();
            return alreadyUploadedFile.LastWriteTimeUtc >= newestFileWriteTime;
        }
    }
}

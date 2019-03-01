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

        private const int CacheExpiryHours = 5;
        public DirectoryInfo DirectoryInfo { get; }

        public string NormalizedDirectoryPath => Helpers.RemoveTrailingSlash(DirectoryInfo.FullName);

        public string ArchiveEntryPrefix { get; }

        public Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            string dirHash;
            using (var hasher = SHA256.Create())
            {
                dirHash = Convert.ToBase64String(hasher.ComputeHash(Encoding.UTF8.GetBytes(NormalizedDirectoryPath)));
                dirHash = dirHash.TrimEnd('='); // base64 url encode it.
                dirHash = dirHash.Replace('+', '-');
                dirHash = dirHash.Replace('/', '_');
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
                    return Task.FromResult(DoUploadAsync(payloadContainer, log).GetAwaiter().GetResult()); // Can't await because of mutex
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

        private async Task<string> DoUploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            await Task.Yield();
            string basePath = NormalizedDirectoryPath;

            var alreadyUploadedFile = new FileInfo(basePath + ".payload");
            if (alreadyUploadedFile.Exists && IsUpToDate(alreadyUploadedFile))
            {
                log?.Invoke($"Using previously uploaded payload for {basePath}");
                return File.ReadAllText(alreadyUploadedFile.FullName);
            }

            log?.Invoke($"Uploading payload for {basePath}");
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
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
            if (alreadyUploadedFile.LastWriteTimeUtc.AddHours(CacheExpiryHours) < DateTime.UtcNow)
            {
                return false;
            }

            var newestFileWriteTime = DirectoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                .Select(file => file.LastWriteTimeUtc)
                .Max();
            return alreadyUploadedFile.LastWriteTimeUtc >= newestFileWriteTime;
        }
    }
}

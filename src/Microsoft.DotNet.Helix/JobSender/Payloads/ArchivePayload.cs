using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class ArchivePayload : IPayload
    {
        private const int CacheExpiryHours = 1;
        public FileInfo Archive { get; }

        public ArchivePayload(string pathToArchive)
        {
            Archive = new FileInfo(pathToArchive);
            if (!Archive.Exists)
            {
                throw new FileNotFoundException($"The file '{pathToArchive}' was not found.");
            }
        }

        public async Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            var alreadyUploadedFile = new FileInfo($"{Archive.FullName}.payload");
            if (alreadyUploadedFile.Exists && IsUpToDate(alreadyUploadedFile))
            {
                log?.Invoke($"Using previously uploaded payload for {Archive.FullName}");
                return File.ReadAllText(alreadyUploadedFile.FullName);
            }

            using (var stream = File.OpenRead(Archive.FullName))
            {
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{Archive.Name}");
                File.WriteAllText(alreadyUploadedFile.FullName, zipUri.AbsoluteUri);
                return zipUri.AbsoluteUri;
            }
        }

        private bool IsUpToDate(FileInfo alreadyUploadedFile)
        {
            return (alreadyUploadedFile.LastWriteTimeUtc.AddHours(CacheExpiryHours) > DateTime.UtcNow) && // Expiration hasn't elapsed
                   (alreadyUploadedFile.LastWriteTimeUtc > Archive.LastWriteTimeUtc);                     // File hasn't changed since it was uploaded
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class ArchivePayload : IPayload
    {
        public FileInfo Archive { get; }

        public ArchivePayload(string pathToArchive)
        {
            Archive = new FileInfo(pathToArchive);
            if (!Archive.Exists)
            {
                throw new FileNotFoundException($"The file '{pathToArchive}' was not found.");
            }
        }

        public async Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            using (var stream = new FileStream(Archive.FullName, FileMode.Open))
            {
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{Archive.Name}");
                return zipUri.AbsoluteUri;
            }
        }
    }
}

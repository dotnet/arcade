using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class AdhocPayload : IPayload
    {
        public AdhocPayload(string[] files)
        {
            Files = files;
        }

        public string[] Files { get; }

        public async Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    foreach (string file in Files)
                    {
                        string name = Path.GetFileName(file);
                        using (Stream entryStream = zip.CreateEntry(name).Open())
                        using (FileStream inputStream = File.OpenRead(file))
                        {
                            await inputStream.CopyToAsync(entryStream);
                        }
                    }
                }
                stream.Position = 0;
                Uri zipUri = await payloadContainer.UploadFileAsync(stream, $"{Guid.NewGuid()}.zip");
                return zipUri.ToString();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        public async Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            if (FindFileNameDuplicate(out var duplicateName))
            {
                throw new FileNameDuplicateException(duplicateName);
            }

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
                return zipUri.AbsoluteUri;
            }
        }

        private bool FindFileNameDuplicate(out string duplicateName)
        {
            var filesSeen = new HashSet<string>();
            duplicateName = Files.FirstOrDefault(file => !filesSeen.Add(Path.GetFileName(file)));
            return duplicateName != null;
        }
    }

    public class FileNameDuplicateException : Exception
    {
        public FileNameDuplicateException(string duplicatedName)
            : base($"Names of files to upload have to be distinct. The following name repeats at least once: {Path.GetFileName(duplicatedName)}") { }
    }
}

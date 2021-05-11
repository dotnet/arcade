// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Arcade.Common
{
    public class ZipArchiveManager : IZipArchiveManager
    {
        public async Task AddResourceFileToArchive<TAssembly>(string archivePath, string resourceName, string targetFileName = null)
        {
            using Stream fileStream = GetResourceFileContent<TAssembly>(resourceName);
            await AddContentToArchive(archivePath, targetFileName ?? resourceName, fileStream);
        }

        public void ArchiveDirectory(string directoryPath, string archivePath, bool includeBaseDirectory)
        {
            ZipFile.CreateFromDirectory(directoryPath, archivePath, CompressionLevel.Fastest, includeBaseDirectory);
        }

        public void ArchiveFile(string filePath, string archivePath)
        {
            using (FileStream fs = File.OpenWrite(archivePath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, false))
            {
                zip.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            }
        }

        private async Task AddContentToArchive(string archivePath, string targetFilename, Stream content)
        {
            using FileStream archiveStream = new FileStream(archivePath, FileMode.Open);
            using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Update);
            ZipArchiveEntry entry = archive.CreateEntry(targetFilename);
            using Stream targetStream = entry.Open();
            await content.CopyToAsync(targetStream);
        }

        private static Stream GetResourceFileContent<TAssembly>(string resourceFileName)
        {
            Assembly assembly = typeof(TAssembly).Assembly;
            return assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{resourceFileName}");
        }
    }
}

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SignTool.Json;

namespace SignTool
{
    /// <summary>
    /// Represents all of the input to the batch signing process.
    /// </summary>
    internal sealed class BatchSignInput
    {
        /// <summary>
        /// The path where the binaries are built to: e:\path\to\source\Binaries\Debug
        /// </summary>
        internal string OutputPath { get; }

        /// <summary>
        /// Uri, to be consumed by later steps, which describes where these files get published to.
        /// </summary>
        internal string PublishUri { get; }

        /// <summary>
        /// The ordered names of the files to be signed. These are all relative paths off of the <see cref="OutputPath"/>
        /// property.
        /// </summary>
        internal ImmutableArray<FileName> FileNames { get; }

        /// <summary>
        /// These are binaries which are included in our zip containers but are already signed. This list is used for 
        /// validation purposes. These are all flat names and cannot be relative paths.
        /// </summary>
        internal ImmutableArray<string> ExternalFileNames { get; }

        /// <summary>
        /// Names of assemblies that need to be signed. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> AssemblyNames { get; }

        /// <summary>
        /// Names of zip containers that need to be examined for signing. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> ZipContainerNames { get; }

        /// <summary>
        /// Names of other file types which aren't specifically handled by the tool. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> OtherNames { get; }

        /// <summary>
        /// A map from all of the binaries that need to be signed to the actual signing data.
        /// </summary>
        internal ImmutableDictionary<FileName, FileSignInfo> FileSignInfoMap { get; }

        private ContentUtil contentUtil = new ContentUtil();

        internal BatchSignInput(string outputPath, Dictionary<string, SignInfo> fileSignDataMap, IEnumerable<string> externalFileNames, string publishUri)
        {
            OutputPath = outputPath;
            PublishUri = publishUri;
            // Use order by to make the output of this tool as predictable as possible.
            var fileNames = fileSignDataMap.Keys;
            FileNames = fileNames.OrderBy(x => x).Select(x => new FileName(outputPath, x)).ToImmutableArray();
            ExternalFileNames = externalFileNames.OrderBy(x => x).ToImmutableArray();

            AssemblyNames = FileNames.Where(x => x.IsAssembly).ToImmutableArray();
            ZipContainerNames = FileNames.Where(x => x.IsZipContainer).ToImmutableArray();
            OtherNames = FileNames.Where(x => !x.IsAssembly && !x.IsZipContainer).ToImmutableArray();

            var builder = ImmutableDictionary.CreateBuilder<FileName, FileSignInfo>();
            foreach (var name in FileNames)
            {
                var data = fileSignDataMap[name.RelativePath];
                builder.Add(name, new FileSignInfo(name, data));
            }
            FileSignInfoMap = builder.ToImmutable();
        }

        internal BatchSignInput(string outputPath, Dictionary<FileSignDataEntry, SignInfo> fileSignDataMap, IEnumerable<string> externalFileNames, string publishUri)
        {
            OutputPath = outputPath;
            PublishUri = publishUri;

            List<FileName> fileNames = fileSignDataMap.Keys.Select(x => new FileName(outputPath, x.FilePath, x.SHA256Hash)).ToList();

            // We need to tolerate not being able to find zips in the same path as they were in on the original machine.
            // As long as we can find a FileName with the same hash for each one, we should be OK.
            ResolveMissingZipArchives(ref fileNames);

            ZipContainerNames = fileNames.Where(x => x.IsZipContainer).ToImmutableArray();
            // If there's any files we can't find, recursively unpack the zip archives we just made a list of above.
            UnpackMissingContent(ref fileNames);
            // After this point, if the files are available execution should be as before.
            // Use OrderBy to make the output of this tool as predictable as possible.
            FileNames = fileNames.OrderBy(x => x.RelativePath).ToImmutableArray();
            ExternalFileNames = externalFileNames.OrderBy(x => x).ToImmutableArray();
            AssemblyNames = FileNames.Where(x => x.IsAssembly).ToImmutableArray();
            OtherNames = FileNames.Where(x => !x.IsAssembly && !x.IsZipContainer).ToImmutableArray();

            var builder = ImmutableDictionary.CreateBuilder<FileName, FileSignInfo>();
            foreach (var name in FileNames)
            {
                var data = fileSignDataMap.Keys.Where(k => k.SHA256Hash == name.SHA256Hash).Single();
                builder.Add(name, new FileSignInfo(name, fileSignDataMap[data]));
            }
            FileSignInfoMap = builder.ToImmutable();
        }

        private void ResolveMissingZipArchives(ref List<FileName> fileNames)
        {
            StringBuilder missingFiles = new StringBuilder();
            List<FileName> missingZips = fileNames.Where(x => x.IsZipContainer && !File.Exists(x.FullPath)).ToList();
            foreach (FileName missingZip in missingZips)
            {
                // Throw if somehow the same missing zip is present more than once. May be OK to take FirstOrDefault.
                var matchingFile = (from path in Directory.GetFiles(OutputPath, missingZip.Name, SearchOption.AllDirectories)
                                    where contentUtil.GetChecksum(path).Equals(missingZip.SHA256Hash, StringComparison.OrdinalIgnoreCase)
                                    select path).SingleOrDefault();

                if (!string.IsNullOrEmpty(matchingFile))
                {
                    fileNames.Remove(missingZip);
                    string relativePath = (new Uri(OutputPath).MakeRelativeUri(new Uri(matchingFile))).OriginalString.Replace('/', Path.DirectorySeparatorChar);
                    FileName updatedFileName = new FileName(OutputPath, relativePath, missingZip.SHA256Hash);
                    fileNames.Add(updatedFileName);
                }
                else
                {
                    missingFiles.AppendLine($"File: {missingZip.Name} Hash: {missingZip.SHA256Hash}");
                }
            }
            if (!(missingFiles.Length == 0))
            {
                throw new FileNotFoundException($"Could not find one or more Zip-archive files referenced:\n{missingFiles}");
            }
        }

        private void UnpackMissingContent(ref List<FileName> candidateFileNames)
        {
            bool success = true;
            string unpackingDirectory = Path.Combine(OutputPath, "UnpackedZipArchives");
            StringBuilder missingFiles = new StringBuilder();
            Directory.CreateDirectory(unpackingDirectory);

            var unpackNeeded = (from file in candidateFileNames
                                where !File.Exists(file.FullPath)
                                select file).ToList();

            // Nothing to do
            if (unpackNeeded.Count() == 0)
                return;

            // Get all Zip Archives in the manifest recursively.
            Queue<FileName> allZipsWeKnowAbout = new Queue<FileName>(ZipContainerNames);

            while (allZipsWeKnowAbout.Count > 0)
            {
                FileName zipFile = allZipsWeKnowAbout.Dequeue();
                string unpackFolder = Path.Combine(unpackingDirectory, zipFile.SHA256Hash);

                // Assumption:  If a zip with a given hash is already unpacked, it's probably OK.
                if (!Directory.Exists(unpackFolder))
                {
                    Directory.CreateDirectory(unpackFolder);
                    ZipFile.ExtractToDirectory(zipFile.FullPath, unpackFolder);
                }
                // Add any zips we just unzipped.
                foreach (string file in Directory.GetFiles(unpackFolder, "*", SearchOption.AllDirectories))
                {
                    if (PathUtil.IsZipContainer(file))
                    {
                        string relativePath = (new Uri(unpackingDirectory).MakeRelativeUri(new Uri(file))).OriginalString;
                        allZipsWeKnowAbout.Enqueue(new FileName(unpackingDirectory, relativePath, contentUtil.GetChecksum(file)));
                    }
                }
            }
            // Lazy : Disks are fast, just calculate ALL hashes.  Could optimize by only files we intend to sign
            Dictionary<string, string> existingHashLookup = new Dictionary<string, string>();
            foreach (string file in Directory.GetFiles(unpackingDirectory, "*", SearchOption.AllDirectories))
            {
                existingHashLookup.Add(file, contentUtil.GetChecksum(file));
            }

            Dictionary<FileName, FileName> fileNameUpdates = new Dictionary<FileName, FileName>();
            // At this point, we've unpacked every Zip we can possibly pull out into folders named for the zip's hash into 'unpackingDirectory'
            foreach (FileName missingFileWithHashToFind in unpackNeeded)
            {
                string matchFile = (from filePath in existingHashLookup.Keys
                                    where Path.GetFileName(filePath).Equals(missingFileWithHashToFind.Name, StringComparison.OrdinalIgnoreCase)
                                    where existingHashLookup[filePath] == missingFileWithHashToFind.SHA256Hash
                                    select filePath).SingleOrDefault();
                if (matchFile == null)
                {
                    success = false;
                    missingFiles.AppendLine($"File: {missingFileWithHashToFind.Name} Hash: '{missingFileWithHashToFind.SHA256Hash}'");
                }
                else
                {
                    string relativePath = (new Uri(OutputPath).MakeRelativeUri(new Uri(matchFile))).OriginalString.Replace('/', Path.DirectorySeparatorChar);
                    FileName updatedFileName = new FileName(OutputPath, relativePath, missingFileWithHashToFind.SHA256Hash);
                    candidateFileNames.Remove(missingFileWithHashToFind);
                    candidateFileNames.Add(updatedFileName);
                }
            }

            if (!success)
            {
                throw new Exception($"Failure attempting to find one or more files:\n{missingFiles.ToString()}");
            }
        }
    }
}


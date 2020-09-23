// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Data for a zip container. Can refer to any zip format such as VSIX, NuPkg, or a raw zip archive.
    /// </summary>
    internal sealed class ZipData
    {
        /// <summary>
        /// Signing information.
        /// </summary>
        internal FileSignInfo FileSignInfo { get; }

        /// <summary>
        /// The parts inside this container which need to be signed.
        /// </summary>
        internal ImmutableArray<ZipPart> NestedParts { get; }

        internal ZipData(FileSignInfo fileSignInfo, ImmutableArray<ZipPart> nestedBinaryParts)
        {
            FileSignInfo = fileSignInfo;
            NestedParts = nestedBinaryParts;
        }

        internal ZipPart? FindNestedPart(string relativeName)
        {
            foreach (var part in NestedParts)
            {
                if (relativeName == part.RelativeName)
                {
                    return part;
                }
            }

            return null;
        }

        /// <summary>
        /// Repack the zip container with the signed files.
        /// </summary>
        public void Repack(TaskLoggingHelper log, string tempDir = null, string wixToolsPath = null)
        {
#if NET472
            if (FileSignInfo.IsVsix())
            {
                RepackPackage(log);
            }
            else
#endif
            {
                if (FileSignInfo.IsWixContainer())
                {
                    RepackWixPack(log, tempDir, wixToolsPath);
                }
                else 
                {
                    RepackRawZip(log);
                }
            }
        }

#if NET472
        /// <summary>
        /// Repack a zip container with a package structure.
        /// </summary>
        private void RepackPackage(TaskLoggingHelper log)
        {
            string getPartRelativeFileName(PackagePart part)
            {
                var path = part.Uri.OriginalString;
                if (!string.IsNullOrEmpty(path) && path[0] == '/')
                {
                    path = path.Substring(1);
                }

                return path;
            }
            
            using (var package = Package.Open(FileSignInfo.FullPath, FileMode.Open, FileAccess.ReadWrite))
            {
                foreach (var part in package.GetParts())
                {
                    var relativeName = getPartRelativeFileName(part);
                    var signedPart = FindNestedPart(relativeName);
                    if (!signedPart.HasValue)
                    {
                        log.LogMessage(MessageImportance.Low, $"Didn't find signed part for nested file: {FileSignInfo.FullPath} -> {relativeName}");
                        continue;
                    }

                    using (var signedStream = File.OpenRead(signedPart.Value.FileSignInfo.FullPath))
                    using (var partStream = part.GetStream(FileMode.Open, FileAccess.ReadWrite))
                    {
                        log.LogMessage(MessageImportance.Low, $"Copying signed stream from {signedPart.Value.FileSignInfo.FullPath} to {FileSignInfo.FullPath} -> {relativeName}.");

                        signedStream.CopyTo(partStream);
                        partStream.SetLength(signedStream.Length);
                    }
                }
            }
        }
#endif
        /// <summary>
        /// Repack raw zip container.
        /// </summary>
        private void RepackRawZip(TaskLoggingHelper log)
        {
            using (var archive = new ZipArchive(File.Open(FileSignInfo.FullPath, FileMode.Open), ZipArchiveMode.Update))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relativeName = entry.FullName;
                    var signedPart = FindNestedPart(relativeName);
                    if (!signedPart.HasValue)
                    {
                        log.LogMessage(MessageImportance.Low, $"Didn't find signed part for nested file: {FileSignInfo.FullPath} -> {relativeName}");
                        continue;
                    }

                    using (var signedStream = File.OpenRead(signedPart.Value.FileSignInfo.FullPath))
                    using (var entryStream = entry.Open())
                    {
                        log.LogMessage(MessageImportance.Low, $"Copying signed stream from {signedPart.Value.FileSignInfo.FullPath} to {FileSignInfo.FullPath} -> {relativeName}.");

                        signedStream.CopyTo(entryStream);
                        entryStream.SetLength(signedStream.Length);
                    }
                }
            }
        }
        private void RepackWixPack(TaskLoggingHelper log, string tempDir, string wixToolsPath)
        {
            if(wixToolsPath == null)
            {
                log.LogError("WixToolsPath must be defined to repack wixpacks. Wixpacks are used to produce signed msi's during release pipeline builds.  If this is not a release pipeline build, you may avoid this error by removing '*.wixpack.zip' from your ItemsToSign.");
                return;
            }

            string workingDir = Path.Combine(tempDir, "extract", Guid.NewGuid().ToString());
            string outputDir = Path.Combine(tempDir, "output");
            ZipFile.ExtractToDirectory(FileSignInfo.WixContentFilePath, workingDir);
            var fileList = Directory.GetFiles(workingDir, "*", SearchOption.AllDirectories);
            foreach(var file in fileList)
            {
                var relativeName = file.Substring($"{workingDir}\\".Length).Replace('\\', '/');
                var signedPart = FindNestedPart(relativeName);
                if (!signedPart.HasValue)
                {
                    log.LogMessage(MessageImportance.Low, $"Didn't find signed part for nested file: {FileSignInfo.FullPath} -> {relativeName}");
                    continue;
                }
                log.LogMessage(MessageImportance.Low, $"Copying signed stream from {signedPart.Value.FileSignInfo.FullPath} to {file}.");
                File.Copy(signedPart.Value.FileSignInfo.FullPath, file, true);
            }
            string createFileName = Path.Combine(workingDir, "create.cmd");
            int exitCode = BatchSignUtil.RunWixTool(createFileName, outputDir, workingDir, wixToolsPath);
            if (exitCode != 0)
            {
                log.LogError($"packaging of wix file '{FileSignInfo.FullPath}' failed");
                return;
            }
            string outputFileName = Path.Combine(outputDir, FileSignInfo.FileName);
            Debug.Assert(File.Exists(outputFileName));
            log.LogMessage($"created wix assembly {outputFileName}");
            log.LogMessage($"replacing '{FileSignInfo.FullPath}' with '{outputFileName}'");
            File.Copy(outputFileName, FileSignInfo.FullPath, true);
        }
    }
}

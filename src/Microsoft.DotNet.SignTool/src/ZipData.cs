// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public void Repack(TaskLoggingHelper log)
        {
#if NET472
            if (FileSignInfo.IsVsix())
            {
                RepackPackage(log);
            }
            else
#endif
            {
                RepackRawZip(log);
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
    }
}

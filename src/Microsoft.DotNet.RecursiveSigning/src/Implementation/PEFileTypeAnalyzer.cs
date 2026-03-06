// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Analyzes PE (Portable Executable) files — .dll, .exe, .sys, etc.
    /// Detects Authenticode signatures by inspecting the PE header's
    /// CertificateTableDirectory entry, matching the approach from SignTool.
    /// </summary>
    public sealed class PEFileTypeAnalyzer : IFileTypeAnalyzer
    {
        private static readonly string[] s_peExtensions = { ".dll", ".exe", ".sys", ".ocx" };

        public bool CanAnalyze(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return s_peExtensions.Any(pe => ext.Equals(pe, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<FileTypeInfo> AnalyzeAsync(
            Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            if (!stream.CanSeek)
            {
                // PEReader needs a seekable stream; copy asynchronously.
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                return AnalyzeCore(ms);
            }

            return Analyze(stream);
        }

        /// <summary>
        /// Synchronously analyzes a seekable PE stream. Separated for testability and
        /// because <see cref="PEReader"/> is not async.
        /// </summary>
        internal static FileTypeInfo Analyze(Stream stream)
        {
            var originalPosition = stream.Position;
            try
            {
                stream.Position = 0;
                return AnalyzeCore(stream);
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        private static FileTypeInfo AnalyzeCore(Stream stream)
        {
            try
            {
                // PEStreamOptions.LeaveOpen so we don't close the caller's stream.
                using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);

                if (peReader.PEHeaders?.PEHeader == null)
                {
                    return FileTypeInfo.Default;
                }

                // Authenticode signature check: the CertificateTableDirectory in the
                // PE header's data directory points to the Authenticode signature.
                // If Size > 0, the file has been signed.
                var certDir = peReader.PEHeaders.PEHeader.CertificateTableDirectory;
                bool isSigned = certDir.Size > 0;

                return new FileTypeInfo(
                    executableType: ExecutableType.PE,
                    isAlreadySigned: isSigned);
            }
            catch (BadImageFormatException)
            {
                // Not a valid PE file despite having a PE extension.
                return FileTypeInfo.Default;
            }
        }
    }
}

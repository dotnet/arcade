// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Non-production signing provider that copies files and appends a dry-run marker.
    /// </summary>
    public sealed class DryRunSigningProvider : ISigningProvider
    {
        public async Task<bool> SignFilesAsync(
            IReadOnlyList<(FileNode node, string outputPath)> files,
            CancellationToken cancellationToken = default)
        {
            foreach (var (node, outputPath) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(node.Location.FilePathOnDisk, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.Copy(node.Location.FilePathOnDisk!, outputPath, overwrite: true);
                }

                await File.AppendAllTextAsync(outputPath, $"\n[DRY-RUN SIGNED with {node.CertificateIdentifier?.Name}]", cancellationToken);
            }

            return true;
        }
    }
}

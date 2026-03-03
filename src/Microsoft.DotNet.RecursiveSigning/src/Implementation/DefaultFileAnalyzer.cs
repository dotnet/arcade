// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Default lightweight file analyzer used by the CLI/demo workflow.
    /// </summary>
    public sealed class DefaultFileAnalyzer : IFileAnalyzer
    {
        public Task<IFileMetadata> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
            }

            return Task.FromResult<IFileMetadata>(new FileMetadata(Path.GetFileName(filePath)));
        }

        public Task<IFileMetadata> AnalyzeAsync(Stream contentStream, string fileName, CancellationToken cancellationToken = default)
        {
            if (contentStream == null)
            {
                throw new ArgumentNullException(nameof(contentStream));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));
            }

            return Task.FromResult<IFileMetadata>(new FileMetadata(fileName));
        }
    }
}

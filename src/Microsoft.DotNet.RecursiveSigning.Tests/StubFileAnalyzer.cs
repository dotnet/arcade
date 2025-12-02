// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Stub file analyzer for Phase 1 testing.
    /// Only calculates SHA-256 hash and extracts filename - no PE analysis.
    /// </summary>
    public sealed class StubFileAnalyzer : IFileAnalyzer
    {
        private readonly IContainerHandlerRegistry _handlerRegistry;
        private readonly IFileSystem _fileSystem;

        public StubFileAnalyzer(IContainerHandlerRegistry handlerRegistry, IFileSystem fileSystem = null!)
        {
            _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<IFileMetadata> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (!_fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}", filePath);
            }

            using (var stream = _fileSystem.GetFileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                await ContentHash.FromStreamAsync(stream, cancellationToken);
                return new FileMetadata(executableType: ExecutableType.None);
            }
        }

        public async Task<IFileMetadata> AnalyzeAsync(Stream contentStream, string fileName, CancellationToken cancellationToken = default)
        {
            if (contentStream == null)
            {
                throw new ArgumentNullException(nameof(contentStream));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            await ContentHash.FromStreamAsync(contentStream, cancellationToken);
            return new FileMetadata(executableType: ExecutableType.None);
        }

        public bool IsContainer(string filePath)
        {
            // Check if any handler can process this file
            var handler = _handlerRegistry.FindHandler(filePath);
            return handler != null;
        }
    }
}

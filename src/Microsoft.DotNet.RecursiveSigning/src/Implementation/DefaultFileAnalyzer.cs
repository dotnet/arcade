// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// File analyzer that dispatches to registered <see cref="IFileTypeAnalyzer"/>
    /// instances based on file name. Falls back to basic filename-only metadata
    /// when no analyzer matches.
    /// </summary>
    public sealed class DefaultFileAnalyzer : IFileAnalyzer
    {
        private readonly IReadOnlyList<IFileTypeAnalyzer> _typeAnalyzers;

        /// <summary>
        /// Creates a file analyzer with no type-specific analyzers (stub mode).
        /// </summary>
        public DefaultFileAnalyzer()
            : this(Array.Empty<IFileTypeAnalyzer>())
        {
        }

        /// <summary>
        /// Creates a file analyzer with the given type-specific analyzers.
        /// </summary>
        public DefaultFileAnalyzer(IEnumerable<IFileTypeAnalyzer> typeAnalyzers)
        {
            _typeAnalyzers = typeAnalyzers?.ToList() ?? throw new ArgumentNullException(nameof(typeAnalyzers));
        }

        public async Task<IFileMetadata> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await AnalyzeAsync(stream, Path.GetFileName(filePath), cancellationToken);
        }

        public async Task<IFileMetadata> AnalyzeAsync(Stream contentStream, string fileName, CancellationToken cancellationToken = default)
        {
            if (contentStream == null)
            {
                throw new ArgumentNullException(nameof(contentStream));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or whitespace.", nameof(fileName));
            }

            var analyzer = FindAnalyzer(fileName);
            if (analyzer == null)
            {
                return new FileMetadata(fileName);
            }

            var info = await analyzer.AnalyzeAsync(contentStream, fileName, cancellationToken);
            return ToMetadata(fileName, info);
        }

        private IFileTypeAnalyzer? FindAnalyzer(string fileName)
        {
            foreach (var analyzer in _typeAnalyzers)
            {
                if (analyzer.CanAnalyze(fileName))
                {
                    return analyzer;
                }
            }
            return null;
        }

        private static FileMetadata ToMetadata(string fileName, FileTypeInfo info) =>
            new(fileName, info.ExecutableType, info.TargetFramework, info.PublicKeyToken, info.IsAlreadySigned);
    }
}

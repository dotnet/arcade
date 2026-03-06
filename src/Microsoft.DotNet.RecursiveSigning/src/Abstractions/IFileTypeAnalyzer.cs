// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Pluggable analyzer for a specific file type (PE, NuGet, etc.).
    /// Registered analyzers are dispatched by <see cref="IFileAnalyzer"/>
    /// based on <see cref="CanAnalyze"/>.
    /// </summary>
    public interface IFileTypeAnalyzer
    {
        /// <summary>
        /// Returns true if this analyzer can handle the given file name
        /// (typically by checking the extension).
        /// </summary>
        bool CanAnalyze(string fileName);

        /// <summary>
        /// Analyze a stream and return file-type-specific metadata.
        /// The stream position is not guaranteed and should be reset if needed.
        /// </summary>
        Task<FileTypeInfo> AnalyzeAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    }
}

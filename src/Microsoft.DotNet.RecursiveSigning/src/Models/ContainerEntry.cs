// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Entry in a container (archive).
    /// </summary>
    public sealed class ContainerEntry : IDisposable
    {
        /// <summary>
        /// Relative path within the container.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Stream to read/write the entry content.
        /// The caller that receives a <see cref="ContainerEntry"/> instance owns this stream and is responsible
        /// for disposing it (typically by disposing the <see cref="ContainerEntry"/>).
        ///
        /// For entries produced by <c>IContainerHandler.ReadEntriesAsync</c>, the stream is only guaranteed to be
        /// valid until the entry is disposed.
        /// </summary>
        public Stream? ContentStream { get; }

        /// <summary>
        /// Content hash (SHA-256).
        /// </summary>
        public byte[]? ContentHash { get; set; }

        /// <summary>
        /// Length of content in bytes.
        /// </summary>
        public long? Length { get; set; }

        /// <summary>
        /// Path to the updated (signed) version of this file.
        /// </summary>
        public string? UpdatedContentPath { get; set; }

        public ContainerEntry(string relativePath, Stream? contentStream)
        {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            ContentStream = contentStream;
        }

        public void Dispose()
        {
            ContentStream?.Dispose();
        }

        public override string ToString() => RelativePath;
    }
}

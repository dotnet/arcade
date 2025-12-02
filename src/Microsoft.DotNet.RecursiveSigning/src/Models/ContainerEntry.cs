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
        /// </summary>
        public Stream ContentStream { get; }

        /// <summary>
        /// Content hash (SHA-256).
        /// </summary>
        public byte[]? ContentHash { get; set; }

        /// <summary>
        /// Length of content in bytes.
        /// </summary>
        public long? Length { get; set; }

        /// <summary>
        /// Whether this entry has been updated with signed content.
        /// </summary>
        public bool IsUpdated { get; set; }

        /// <summary>
        /// Path to the updated (signed) version of this file.
        /// </summary>
        public string? UpdatedContentPath { get; set; }

        public ContainerEntry(string relativePath, Stream contentStream)
        {
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            ContentStream = contentStream ?? throw new ArgumentNullException(nameof(contentStream));
        }

        public void Dispose()
        {
            ContentStream?.Dispose();
        }

        public override string ToString() => RelativePath;
    }
}

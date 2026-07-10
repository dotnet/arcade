// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks.FileCatalog
{
    /// <summary>
    /// A single file (or in-memory blob) to include in a catalog.
    /// </summary>
    public sealed class CatalogEntry
    {
        /// <summary>
        /// Creates an entry whose content is loaded from <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">Path on disk to read for the file content/hash.</param>
        /// <param name="name">
        /// Optional logical name for the entry. Defaults to <see cref="Path.GetFileName(string)"/>
        /// of <paramref name="filePath"/>. The name is not embedded in the catalog (makecat
        /// V2 catalogs do not carry per-member file names); it is retained only for diagnostics.
        /// </param>
        public static CatalogEntry FromFile(string filePath, string? name = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("File path must not be null or empty.", nameof(filePath));
            }

            byte[] content = File.ReadAllBytes(filePath);
            return new CatalogEntry(name ?? Path.GetFileName(filePath), content);
        }

        /// <summary>
        /// Creates an entry from in-memory content.
        /// </summary>
        public CatalogEntry(string name, ReadOnlyMemory<byte> content)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Name must not be null or empty.", nameof(name));
            }

            Name = name;
            Content = content;
        }

        /// <summary>Logical name for the entry (typically the file name). Not embedded in the catalog.</summary>
        public string Name { get; }

        /// <summary>The raw bytes whose hash will be recorded in the catalog.</summary>
        public ReadOnlyMemory<byte> Content { get; }
    }
}

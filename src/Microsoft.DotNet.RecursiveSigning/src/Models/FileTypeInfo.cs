// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// File-type-specific information returned by <see cref="Abstractions.IFileTypeAnalyzer"/>.
    /// </summary>
    public sealed class FileTypeInfo
    {
        /// <summary>
        /// The executable type detected (PE, ELF, MachO, or None).
        /// </summary>
        public ExecutableType ExecutableType { get; }

        /// <summary>
        /// True if the file already has a code signature (e.g., Authenticode for PE).
        /// </summary>
        public bool IsAlreadySigned { get; }

        /// <summary>
        /// Target framework moniker, if applicable (e.g., from a managed PE assembly).
        /// </summary>
        public string? TargetFramework { get; }

        /// <summary>
        /// Public key token, if applicable (e.g., from a managed PE assembly).
        /// </summary>
        public string? PublicKeyToken { get; }

        public FileTypeInfo(
            ExecutableType executableType = ExecutableType.None,
            bool isAlreadySigned = false,
            string? targetFramework = null,
            string? publicKeyToken = null)
        {
            ExecutableType = executableType;
            IsAlreadySigned = isAlreadySigned;
            TargetFramework = targetFramework;
            PublicKeyToken = publicKeyToken;
        }

        /// <summary>
        /// Default instance for files with no type-specific information.
        /// </summary>
        public static FileTypeInfo Default { get; } = new();
    }
}

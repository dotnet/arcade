// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Metadata about a file in the signing workflow.
    /// </summary>
    public sealed class FileMetadata : IFileMetadata
    {
        public string FileName { get; }

        public ExecutableType ExecutableType { get; }

        public string? TargetFramework { get; }

        public string? PublicKeyToken { get; }

        public bool IsAlreadySigned { get; }

        public FileMetadata(
            string fileName,
            ExecutableType executableType = ExecutableType.None,
            string? targetFramework = null,
            string? publicKeyToken = null,
            bool isAlreadySigned = false)
        {
            FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            ExecutableType = executableType;
            TargetFramework = targetFramework;
            PublicKeyToken = publicKeyToken;
            IsAlreadySigned = isAlreadySigned;
        }

        public override string ToString()
        {
            return $"{ExecutableType}, TFM: {TargetFramework ?? "<none>"}, PKT: {PublicKeyToken ?? "<none>"}, AlreadySigned: {IsAlreadySigned}";
        }
    }
}

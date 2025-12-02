// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Intrinsic file properties independent of where the file was found or stored.
    /// </summary>
    public interface IFileMetadata
    {
        ExecutableType ExecutableType { get; }

        string? TargetFramework { get; }

        string? PublicKeyToken { get; }
    }
}

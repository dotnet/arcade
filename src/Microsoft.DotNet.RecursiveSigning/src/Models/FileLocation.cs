// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Location of a file: where it exists on disk and (optionally) where it appears within a container.
    /// </summary>
    public sealed record FileLocation(
        string? FilePathOnDisk,
        string? RelativePathInContainer);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Internal.Utilities;

public class SourceBuildInfo
{
    /// <summary>
    /// Name of the repository during the source-build.
    /// </summary>
    public string RepoName { get; set; }

    /// <summary>
    /// Indicates whether a dependency depends only on managed inputs.
    /// </summary>
    public bool ManagedOnly { get; set; }

    /// <summary>
    /// Indicates whether a dependency is only used in the tarball builds.
    /// </summary>
    public bool TarballOnly { get; set; }
}

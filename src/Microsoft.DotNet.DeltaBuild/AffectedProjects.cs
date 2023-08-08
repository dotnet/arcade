// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Arcade.Common;

namespace Microsoft.DotNet.DeltaBuild;

public record AffectedProjects(
    List<FilePath> DirectlyAffectedProjects,
    List<FilePath> UpstreamTree,
    List<FilePath> DownstreamTree)
{
    public AffectedProjects MergeWith(AffectedProjects affectedProjects)
    {
        return new AffectedProjects(
            affectedProjects.DirectlyAffectedProjects.Union(DirectlyAffectedProjects).Distinct().ToList(),
            affectedProjects.UpstreamTree.Union(UpstreamTree).Distinct().ToList(),
            affectedProjects.DownstreamTree.Union(DownstreamTree).Distinct().ToList());
    }

    public AffectedProjects ChangeRoot(
        IFileSystem fileSystem,
        DirectoryInfo oldRoot,
        DirectoryInfo newRoot)
    {
        return new AffectedProjects(
            ChangeRootWithSelector(x => x.DirectlyAffectedProjects, fileSystem, oldRoot, newRoot),
            ChangeRootWithSelector(x => x.UpstreamTree, fileSystem, oldRoot, newRoot),
            ChangeRootWithSelector(x => x.DownstreamTree, fileSystem, oldRoot, newRoot));
    }

    private List<FilePath> ChangeRootWithSelector(
        Func<AffectedProjects, List<FilePath>> selector,
        IFileSystem fileSystem,
        DirectoryInfo oldRoot,
        DirectoryInfo newRoot)
    {
        return selector(this)
            .Select(x => x.ChangeRoot(fileSystem, oldRoot, newRoot))
            .Where(x => x.Exists)
            .Distinct()
            .ToList();
    }
}

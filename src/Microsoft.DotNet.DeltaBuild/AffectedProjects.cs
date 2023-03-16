// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DeltaBuild;

internal record AffectedProjects(
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

    public AffectedProjects ChangeRoot(DirectoryInfo oldRoot, DirectoryInfo newRoot)
    {
        return new AffectedProjects(
            ChangeRootWithSelector(x => x.DirectlyAffectedProjects, oldRoot, newRoot),
            ChangeRootWithSelector(x => x.UpstreamTree, oldRoot, newRoot),
            ChangeRootWithSelector(x => x.DownstreamTree, oldRoot, newRoot));
    }

    private List<FilePath> ChangeRootWithSelector(Func<AffectedProjects, List<FilePath>> selector, DirectoryInfo oldRoot, DirectoryInfo newRoot)
    {
        return selector(this)
            .Select(x => x.ChangeRoot(oldRoot, newRoot))
            .Where(x => x.Exists)
            .Distinct()
            .ToList();
    }
}

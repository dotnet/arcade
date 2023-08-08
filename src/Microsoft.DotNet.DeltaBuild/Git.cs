// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using LibGit2Sharp;

namespace Microsoft.DotNet.DeltaBuild;

[ExcludeFromCodeCoverage]
internal static class Git
{
    public static readonly List<ChangeKind> UnchangedStatuses = new()
    {
        ChangeKind.Unmodified,
        ChangeKind.Untracked,
        ChangeKind.Ignored,
        ChangeKind.Unreadable
    };

    public static TreeChanges Diff(string repositoryPath, string? remoteBranchName)
    {
        if (string.IsNullOrWhiteSpace(remoteBranchName))
        {
            remoteBranchName = "origin/main";
        }

        using var repository = new Repository(repositoryPath);

        var mainJoinBranch = repository.ObjectDatabase.FindMergeBase(
            repository.Branches[remoteBranchName].Tip, repository.Head.Tip);

        return repository.Diff.Compare<TreeChanges>(
            mainJoinBranch.Tree,
            repository.Head.Tip.Tree);
    }
}

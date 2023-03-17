// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.Collections.Generic;
using LibGit2Sharp;

namespace Microsoft.DotNet.DeltaBuild;

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

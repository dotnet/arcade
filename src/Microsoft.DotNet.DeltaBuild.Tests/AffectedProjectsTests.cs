// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Arcade.Test.Common;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class AffectedProjectsTests
{
    [Fact]
    public void AffectedProjects_Construction_CreatesCorrectly()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/root1/dir1/proj1.csproj"), "" },
            { Path.GetFullPath("/root1/dir1/proj2.csproj"), "" },
            { Path.GetFullPath("/root1/dir2/proj3.csproj"), "" }
        });

        var directlyAffected = new List<FilePath> {
            FilePath.Create(fileSystem, "/root1/dir1/proj1.csproj"),
            FilePath.Create(fileSystem, "/root1/dir1/proj2.csproj")
        };

        var upstreamTree = new List<FilePath> { FilePath.Create(fileSystem, "/root1/dir2/proj3.csproj") };
        var downstreamTree = new List<FilePath>();

        // Act
        var affectedProjects = new AffectedProjects(directlyAffected, upstreamTree, downstreamTree);

        // Assert
        Assert.Equal(directlyAffected, affectedProjects.DirectlyAffectedProjects);
        Assert.Equal(upstreamTree, affectedProjects.UpstreamTree);
        Assert.Equal(downstreamTree, affectedProjects.DownstreamTree);
    }

    [Fact]
    public void MergeWith_CombinesAffectedProjectsCorrectly()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/root1/dir1/proj1.csproj"), "" },
            { Path.GetFullPath("/root1/dir1/proj2.csproj"), "" },
            { Path.GetFullPath("/root1/dir2/proj3.csproj"), "" },
            { Path.GetFullPath("/root2/dir1/proj4.csproj"), "" },
            { Path.GetFullPath("/root2/dir1/proj5.csproj"), "" },
            { Path.GetFullPath("/root2/dir2/proj6.csproj"), "" }
        });

        var directlyAffected1 = new List<FilePath> {
            FilePath.Create(fileSystem, "/root1/dir1/proj1.csproj"),
            FilePath.Create(fileSystem, "/root1/dir1/proj2.csproj")
        };
        var upstreamTree1 = new List<FilePath> { FilePath.Create(fileSystem, "/root1/dir2/proj3.csproj") };
        var downstreamTree1 = new List<FilePath>();

        var directlyAffected2 = new List<FilePath> {
            FilePath.Create(fileSystem, "/root2/dir1/proj4.csproj"),
            FilePath.Create(fileSystem, "/root2/dir1/proj5.csproj")
        };
        var upstreamTree2 = new List<FilePath> { FilePath.Create(fileSystem, "/root2/dir2/proj6.csproj") };
        var downstreamTree2 = new List<FilePath>();

        var affectedProjects1 = new AffectedProjects(directlyAffected1, upstreamTree1, downstreamTree1);
        var affectedProjects2 = new AffectedProjects(directlyAffected2, upstreamTree2, downstreamTree2);

        // Act
        var mergedProjects = affectedProjects1.MergeWith(affectedProjects2);

        // Assert
        Assert.Equal(
            directlyAffected1.Union(directlyAffected2).OrderBy(p => p.FullPath).ToList(),
            mergedProjects.DirectlyAffectedProjects.OrderBy(p => p.FullPath).ToList());

        Assert.Equal(
            upstreamTree1.Union(upstreamTree2).OrderBy(p => p.FullPath).ToList(),
            mergedProjects.UpstreamTree.OrderBy(p => p.FullPath).ToList());

        Assert.Equal(
            downstreamTree1.Union(downstreamTree2).OrderBy(p => p.FullPath).ToList(),
            mergedProjects.DownstreamTree.OrderBy(p => p.FullPath).ToList());
    }

    [Fact]
    public void ChangeRoot_ChangesRootCorrectly()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/oldRoot/directlyAffected1"), "" },
            { Path.GetFullPath("/oldRoot/upstream1"), "" },
            { Path.GetFullPath("/oldRoot/downstream1"), "" },
            { Path.GetFullPath("/newRoot/directlyAffected1"), "" },  // add these new paths
            { Path.GetFullPath("/newRoot/upstream1"), "" },
            { Path.GetFullPath("/newRoot/downstream1"), "" }
        });

        var directlyAffected = new List<FilePath> { FilePath.Create(fileSystem, "/oldRoot/directlyAffected1") };
        var upstreamTree = new List<FilePath> { FilePath.Create(fileSystem, "/oldRoot/upstream1") };
        var downstreamTree = new List<FilePath> { FilePath.Create(fileSystem, "/oldRoot/downstream1") };

        var affectedProjects = new AffectedProjects(directlyAffected, upstreamTree, downstreamTree);

        var oldRoot = new DirectoryInfo("/oldRoot");
        var newRoot = new DirectoryInfo("/newRoot");

        // Act
        var changedProjects = affectedProjects.ChangeRoot(fileSystem, oldRoot, newRoot);

        // Assert
        Assert.Equal(
            Path.GetFullPath("/newRoot/directlyAffected1"),
            changedProjects.DirectlyAffectedProjects.Single().FullPath);

        Assert.Equal(
            Path.GetFullPath("/newRoot/upstream1"),
            changedProjects.UpstreamTree.Single().FullPath);

        Assert.Equal(
            Path.GetFullPath("/newRoot/downstream1"),
            changedProjects.DownstreamTree.Single().FullPath);
    }
}

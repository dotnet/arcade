// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class DependencyResolverTests
{
    [Fact]
    public void GetDownstreamDependencies_ReturnsCorrectDependencies()
    {
        // Arrange
        var project1 = FilePath.Create("project1");
        var project2 = FilePath.Create("project2");
        var project3 = FilePath.Create("project3");
        var project4 = FilePath.Create("project4");

        var projects = new Dictionary<FilePath, HashSet<FilePath>>
        {
            {project1, new HashSet<FilePath>{project2}},
            {project2, new HashSet<FilePath>{project3}},
            {project3, new HashSet<FilePath>{project4}},
            {project4, new HashSet<FilePath>()},
        };

        // Act
        var dependencies = DependencyResolver.GetDownstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Equal(3, dependencies.Count);
        Assert.Contains(project2, dependencies);
        Assert.Contains(project3, dependencies);
        Assert.Contains(project4, dependencies);
    }

    [Fact]
    public void GetDownstreamDependencies_ReturnsEmptyForProjectWithoutDependencies()
    {
        // Arrange
        var project1 = FilePath.Create("project1");

        var projects = new Dictionary<FilePath, HashSet<FilePath>>
        {
            {project1, new HashSet<FilePath>()},
        };

        // Act
        var dependencies = DependencyResolver.GetDownstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetDownstreamDependencies_ReturnsEmptyWhenProjectNotInDictionary()
    {
        // Arrange
        var projects = new Dictionary<FilePath, HashSet<FilePath>>();
        var wanted = FilePath.Create("wanted");

        // Act
        Assert.Throws<KeyNotFoundException>(() => DependencyResolver.GetDownstreamDependencies(projects, wanted).ToList());
    }

    [Fact]
    public void GetUpstreamDependencies_ReturnsCorrectDependencies()
    {
        // Arrange
        var project1 = FilePath.Create("project1");
        var project2 = FilePath.Create("project2");
        var project3 = FilePath.Create("project3");
        var project4 = FilePath.Create("project4");

        var projects = new Dictionary<FilePath, HashSet<FilePath>>
        {
            {project1, new HashSet<FilePath>()},
            {project2, new HashSet<FilePath>{project1}},
            {project3, new HashSet<FilePath>{project2}},
            {project4, new HashSet<FilePath>{project3}},
        };

        // Act
        var dependencies = DependencyResolver.GetUpstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Equal(3, dependencies.Count);
        Assert.Contains(project2, dependencies);
        Assert.Contains(project3, dependencies);
        Assert.Contains(project4, dependencies);
    }

    [Fact]
    public void GetUpstreamDependencies_ReturnsEmptyForProjectWithoutDependencies()
    {
        // Arrange
        var project1 = FilePath.Create("project1");

        var projects = new Dictionary<FilePath, HashSet<FilePath>>
        {
            {project1, new HashSet<FilePath>()},
        };

        // Act
        var dependencies = DependencyResolver.GetUpstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetUpstreamDependencies_ReturnsEmptyForEmptyProjects()
    {
        // Arrange
        var projects = new Dictionary<FilePath, HashSet<FilePath>>();
        var project1 = FilePath.Create("project1");

        // Act
        var dependencies = DependencyResolver.GetUpstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Empty(dependencies);
    }

    [Fact]
    public void GetUpstreamDependencies_ReturnsEmptyWhenProjectIsNotDependency()
    {
        // Arrange
        var project1 = FilePath.Create("project1");
        var project2 = FilePath.Create("project2");

        var projects = new Dictionary<FilePath, HashSet<FilePath>>
        {
            {project1, new HashSet<FilePath>()},
            {project2, new HashSet<FilePath>()},
        };

        // Act
        var dependencies = DependencyResolver.GetUpstreamDependencies(projects, project1).ToList();

        // Assert
        Assert.Empty(dependencies);
    }
}

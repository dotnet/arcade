// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class MsBuildExtensionsTests
{
    [Fact]
    public void GetRootDirectory_ReturnsCorrectDirectory()
    {
        // Arrange
        var build = new Build.Logging.StructuredLogger.Build();
        const string projectDirectory = "/path/to/project";
        var expectedDirectory = new DirectoryInfo(projectDirectory);

        var project = new Project { ProjectFile = Path.Combine(projectDirectory, "foo.csproj") };
        build.AddChild(project);

        // Act
        var actualDirectory = build.GetRootDirectory();

        // Assert
        Assert.Equal(expectedDirectory.FullName, actualDirectory.FullName);
    }

    [Fact]
    public void GetRootDirectory_ThrowsException_WhenNoRootDirectory()
    {
        // Arrange
        var build = new Build.Logging.StructuredLogger.Build();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => build.GetRootDirectory());
        Assert.Equal("Could not determine root directory of the project.", exception.Message);
    }

    [Fact]
    public void CreateProjectProperties_ShouldReturnCorrectProjectProperties()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        var mockProjectEvaluation = new ProjectEvaluation();
        var propertiesFolder = new Folder { Name = "Properties" };

        propertiesFolder.AddChild(new Property
        {
            Name = "MSBuildProjectFullPath",
            Value = "/full/path/to/project"
        });

        propertiesFolder.AddChild(new Property
        {
            Name = "MSBuildProjectDirectory",
            Value = "/directory/to/project"
        });

        mockProjectEvaluation.Children.Add(propertiesFolder);

        // Act
        var projectProperties = mockProjectEvaluation.CreateProjectProperties(repositoryPath);

        // Assert
        Assert.Equal(repositoryPath.FullName, projectProperties.RepositoryPath);
        Assert.Equal("/full/path/to/project", projectProperties.ProjectFullPath);
        Assert.Equal("/directory/to/project", projectProperties.ProjectDirectory);
    }

    [Fact]
    public void CreateProjectProperties_ShouldThrowWhenPropertiesFolderNotFound()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");
        var mockProjectEvaluation = new ProjectEvaluation();

        // Act and Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            mockProjectEvaluation.CreateProjectProperties(repositoryPath));

        Assert.NotNull(exception);
    }

    [Fact]
    public void ExtractImports_ReturnsCorrectImports()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var importsNode = new TimedNode();

        importsNode.AddChild(new Import { Name = "import1.csproj" });
        importsNode.AddChild(new Import { Name = "import2.csproj" });
        importsNode.AddChild(new Import { Name = "import3.csproj" });

        var expected = new List<FilePath>
        {
            FilePath.Create("import1.csproj", projectDirectory),
            FilePath.Create("import2.csproj", projectDirectory),
            FilePath.Create("import3.csproj", projectDirectory)
        };

        // Act
        var result = importsNode.ExtractImports(properties).ToList();

        // Assert
        Assert.Equal(expected.Count, result.Count);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(expected[i].FullPath, result[i].FullPath);
        }
    }

    [Fact]
    public void ExtractImports_SkipsImportsOutsideRepositoryRoot()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var importsNode = new TimedNode();
        importsNode.AddChild(new Import { Name = "/otherRepo/import1.csproj" }); // outside the repo root

        // Act
        var result = importsNode.ExtractImports(properties).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractImports_ReturnsNestedImports()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var importsNode = new TimedNode();
        var childNode = new TimedNode();
        childNode.AddChild(new Import { Name = "nestedImport1.csproj" });  // nested import

        importsNode.AddChild(new Import { Name = "import1.csproj" });
        importsNode.AddChild(childNode); // add child node with nested import

        var expected = new List<FilePath>
        {
            FilePath.Create("import1.csproj", projectDirectory),
            FilePath.Create("nestedImport1.csproj", projectDirectory)
        };

        // Act
        var result = importsNode.ExtractImports(properties).ToList();

        // Assert
        Assert.Equal(expected.Count, result.Count);

        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(expected[i].FullPath, result[i].FullPath);
        }
    }

    [Fact]
    public void ExtractProjectReferences_WhenNoProjectReferences_ShouldReturnEmpty()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var folder = new Folder();

        // Act
        var result = folder.ExtractProjectReferences(properties).ToList();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractProjectReferences_WhenAllReferencesInRepo_ShouldReturnAll()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var folder = new Folder();
        var projectReferences = new AddItem { Name = "ProjectReference" };
        projectReferences.Children.Add(new Item { Text = "Project2" });
        projectReferences.Children.Add(new Item { Text = "Project3" });
        folder.Children.Add(projectReferences);

        // Act
        var result = folder.ExtractProjectReferences(properties).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(FilePath.Create("Project2", projectDirectory).FullPath, result[0].FullPath);
        Assert.Equal(FilePath.Create("Project3", projectDirectory).FullPath, result[1].FullPath);
    }

    [Fact]
    public void ExtractProjectReferences_WhenSomeReferencesOutsideRepo_ShouldReturnOnlyInRepo()
    {
        // Arrange
        var repositoryPath = new DirectoryInfo("/repo");

        const string projectFullPath = "/repo/src/Project1";
        const string projectDirectory = "/repo/src";

        var properties = new ProjectProperties(
            repositoryPath.FullName,
            projectFullPath,
            projectDirectory);

        var folder = new Folder();
        var projectReferences = new AddItem { Name = "ProjectReference" };
        projectReferences.Children.Add(new Item { Text = "Project2" });
        projectReferences.Children.Add(new Item { Text = "/otherRepo/Project3" });  // outside the repo
        projectReferences.Children.Add(new Item { Text = "/repo/src/Project4" });
        folder.Children.Add(projectReferences);

        // Act
        var result = folder.ExtractProjectReferences(properties).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(FilePath.Create("Project2", projectDirectory).FullPath, result[0].FullPath);
        Assert.Equal(FilePath.Create("Project4", projectDirectory).FullPath, result[1].FullPath);
    }

    [Fact]
    public void ExtractFileGroup_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "Test" };

        addItem.Children.Add(new Item { Text = "testFile1.cs" });
        addItem.Children.Add(new Item { Text = "testFile2.cs" });

        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractFileGroup(properties, "Test").ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(FilePath.Create("/directory/to/project/testFile1.cs"), result[0].FullPath);
        Assert.Equal(FilePath.Create("/directory/to/project/testFile2.cs"), result[1].FullPath);
    }

    [Fact]
    public void ExtractFileGroup_ReturnsEmpty_WhenNoAddItemWithGivenName()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "OtherTest" };  // AddItem has different name

        addItem.Children.Add(new Item { Text = "testFile1.cs" });
        addItem.Children.Add(new Item { Text = "testFile2.cs" });

        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractFileGroup(properties, "Test").ToList(); // "Test" name does not exist

        // Assert
        Assert.Empty(result);  // Expecting no results
    }

    [Fact]
    public void ExtractFileGroup_ReturnsEmpty_WhenAddItemHasNoChildren()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "Test" };  // AddItem with correct name but no children

        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractFileGroup(properties, "Test").ToList();

        // Assert
        Assert.Empty(result);  // Expecting no results
    }

    [Fact]
    public void ExtractAdditionalFiles_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "AdditionalFiles" };
        addItem.Children.Add(new Item { Text = "additionalFile1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractAdditionalFiles(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/additionalFile1.cs"), result[0].FullPath);
    }

    [Fact]
    public void ExtractCompile_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "Compile" };
        addItem.Children.Add(new Item { Text = "compileFile1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractCompile(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/compileFile1.cs"), result[0].FullPath);
    }

    [Fact]
    public void ExtractNone_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "None" };
        addItem.Children.Add(new Item { Text = "noneFile1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractNone(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/noneFile1.cs"), result[0].FullPath);
    }

    [Fact]
    public void ExtractGlobalAnalyzerConfigFiles_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "GlobalAnalyzerConfigFiles" };
        addItem.Children.Add(new Item { Text = "analyzerConfig1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractGlobalAnalyzerConfigFiles(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/analyzerConfig1.cs"), result[0].FullPath);
    }

    [Fact]
    public void ExtractEditorConfigFiles_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "EditorConfigFiles" };
        addItem.Children.Add(new Item { Text = "editorConfig1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractEditorConfigFiles(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/editorConfig1.cs"), result[0].FullPath);
    }

    [Fact]
    public void ExtractPotentialEditorConfigFiles_ReturnsCorrectFilePaths()
    {
        // Arrange
        var properties = new ProjectProperties("/repo", "/full/path/to/project", "/directory/to/project");
        var folder = new Folder();
        var addItem = new AddItem { Name = "PotentialEditorConfigFiles" };
        addItem.Children.Add(new Item { Text = "potentialEditorConfig1.cs" });
        folder.Children.Add(addItem);

        // Act
        var result = folder.ExtractPotentialEditorConfigFiles(properties).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(FilePath.Create("/directory/to/project/potentialEditorConfig1.cs"), result[0].FullPath);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.DotNet.DeltaBuild;

public static class MsBuildExtensions
{
    public static DirectoryInfo GetRootDirectory(this Build.Logging.StructuredLogger.Build build)
    {
        var project = build.FindChild<Project>();
        if (project == null || string.IsNullOrWhiteSpace(project.ProjectFile))
        {
            throw new InvalidOperationException("Could not determine root directory of the project.");
        }

        return new DirectoryInfo(project.ProjectDirectory);
    }

    public static ProjectProperties CreateProjectProperties(
        this ProjectEvaluation project, DirectoryInfo repositoryPath)
    {
        var properties = project.FindChild<Folder>("Properties");
        if (properties is null)
        {
            throw new ArgumentException(nameof(properties));
        }

        return new(
            repositoryPath.FullName,
            properties.GetProjectFullPath()!,
            properties.GetProjectDirectory()!);
    }

    public static IEnumerable<FilePath> ExtractImports(
        this TimedNode imports, ProjectProperties properties)
    {
        var items = imports.FindChildrenRecursive<Import>();
        foreach (var import in items)
        {
            var filePath = GetRootedFilePath(properties, import.Name);
            if (!filePath.FullPath.StartsWith(properties.RepositoryPath))
            {
                continue;
            }

            yield return filePath;
        }
    }

    public static IEnumerable<FilePath> ExtractProjectReferences(
        this Folder folder, ProjectProperties properties)
    {
        var items = folder.FindChild<AddItem>("ProjectReference");
        if (items is null)
        {
            yield break;
        }

        foreach (var item in items.Children.Cast<Item>())
        {
            var filePath = GetRootedFilePath(properties, item);

            // Referenced project is outside of repository.
            if (!filePath.FullPath.StartsWith(properties.RepositoryPath))
            {
                continue;
            }

            yield return filePath;
        }
    }

    public static IEnumerable<FilePath> ExtractAdditionalFiles(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "AdditionalFiles");

    public static IEnumerable<FilePath> ExtractCompile(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "Compile");

    public static IEnumerable<FilePath> ExtractNone(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "None");

    public static IEnumerable<FilePath> ExtractGlobalAnalyzerConfigFiles(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "GlobalAnalyzerConfigFiles");

    public static IEnumerable<FilePath> ExtractEditorConfigFiles(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "EditorConfigFiles");

    public static IEnumerable<FilePath> ExtractPotentialEditorConfigFiles(
        this Folder folder, ProjectProperties properties) =>
        folder.ExtractFileGroup(properties, "PotentialEditorConfigFiles");

    public static IEnumerable<FilePath> ExtractFileGroup(
        this TreeNode folder, ProjectProperties properties, string name)
    {
        var items = folder.FindChild<AddItem>(name);
        if (items is null)
        {
            yield break;
        }

        foreach (var item in items.Children.Cast<Item>())
        {
            yield return GetRootedFilePath(properties, item);
        }
    }

    private static string? GetProjectFullPath(this Folder folder) =>
        folder.GetProperty("MSBuildProjectFullPath");

    private static string? GetProjectDirectory(this Folder folder) =>
        folder.GetProperty("MSBuildProjectDirectory");

    private static FilePath GetRootedFilePath(ProjectProperties properties, Item item) =>
        GetRootedFilePath(properties, item.Text);

    private static FilePath GetRootedFilePath(ProjectProperties properties, string path) =>
        FilePath.Create(path, properties.ProjectDirectory);

    private static string? GetProperty(this TreeNode folder, string name) =>
        folder.FindChild<Property>(i => i.Name == name)?.Value;
}

// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

using TreeNode = Microsoft.Build.Logging.StructuredLogger.TreeNode;

namespace Microsoft.DotNet.DeltaBuild;

internal static class MsBuild
{
    public static string? GetProjectFullPath(this Folder folder) =>
        folder.GetProperty("MSBuildProjectFullPath");

    public static string? GetProjectDirectory(this Folder folder) =>
        folder.GetProperty("MSBuildProjectDirectory");

    public static string? GetNuGetPackageRoot(this Folder folder) =>
        folder.GetProperty("NuGetPackageRoot");

    public static string? GetProjectExtensionsPath(this Folder folder) =>
        folder.GetProperty("MSBuildProjectExtensionsPath");

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

    private static IEnumerable<FilePath> ExtractFileGroup(
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

    private static FilePath GetRootedFilePath(ProjectProperties properties, Item item) =>
        GetRootedFilePath(properties, item.Text);

    private static FilePath GetRootedFilePath(ProjectProperties properties, string path) =>
        FilePath.Create(path, properties.ProjectDirectory);

    private static string? GetProperty(this TreeNode folder, string name) =>
        folder.FindChild<Property>(i => i.Name == name)?.Value;
}

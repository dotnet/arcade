// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.DotNet.DeltaBuild;

internal record ProjectProperties(
    string RepositoryPath,
    string ProjectFullPath,
    string ProjectDirectory,
    string NuGetPackageRoot,
    string ProjectExtensionsPath);

internal static class ProjectPropertiesExtensions
{
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
            properties.GetProjectDirectory()!,
            properties.GetNuGetPackageRoot()!,
            properties.GetProjectExtensionsPath()!);
    }
}

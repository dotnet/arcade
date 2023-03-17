// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System.IO;
using Microsoft.Build.Logging.StructuredLogger;

namespace Microsoft.DotNet.DeltaBuild;

internal static class BuildExtension
{
    public static DirectoryInfo GetRootDirectory(
        this Build.Logging.StructuredLogger.Build build)
    {
        var project = build.FindChild<Project>();
        return new DirectoryInfo(project.ProjectDirectory);
    }
}

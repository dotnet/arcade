// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.DeltaBuild;

public static class DependencyResolver
{
    public static IEnumerable<FilePath> GetDownstreamDependencies(
        IDictionary<FilePath, HashSet<FilePath>> projects, FilePath wanted)
    {
        foreach (var project in projects[wanted])
        {
            // Directly referenced projects.
            yield return project;

            // Transitively referenced projects.
            foreach (var downstreamDependency in GetDownstreamDependencies(projects, project))
            {
                yield return downstreamDependency;
            }
        }
    }

    public static IEnumerable<FilePath> GetUpstreamDependencies(
        IDictionary<FilePath, HashSet<FilePath>> projects, FilePath wanted)
    {
        foreach (var project in projects)
        {
            // Project does not depend on a wanted project.
            if (!project.Value.Contains(wanted))
            {
                continue;
            }

            // Project does depend on wanted project.
            yield return project.Key;

            // All its upstream dependencies depend on it by extension.
            foreach (var upstreamProject in GetUpstreamDependencies(projects, project.Key))
            {
                yield return upstreamProject;
            }
        }
    }
}

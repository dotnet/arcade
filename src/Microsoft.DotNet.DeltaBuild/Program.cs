// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.DeltaBuild;
using Microsoft.Build.Logging.StructuredLogger;

internal static class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private static async Task<int> Main(string[] args)
    {
        return await ProgramArgs.ParseAndRunAsync(ExecuteAsync, args);
    }

    private static int ExecuteAsync(ProgramArgs args)
    {
        // Write at the start because it takes time for the command to finish.
        Console.WriteLine("Running DeltaBuild. This may take some time.");

        if (!args.BranchBinLog?.Exists ?? false)
        {
            Console.Error.WriteLine("Branch binary log {0} does not exist!", args.BranchBinLog!.FullName);
            return -1;
        }

        if (!args.BinLog.Exists)
        {
            Console.Error.WriteLine("Binary log {0} does not exist!", args.BinLog.FullName);
            return -1;
        }

        if (!args.Repository.Exists)
        {
            Console.Error.WriteLine("Repository path {0} does not exist!", args.Repository.FullName);
            return -1;
        }

        // Print debug information.
        if (args.Debug)
        {
            Console.WriteLine("Binary log: {0}", args.BinLog.FullName);
            Console.WriteLine("Branch binary log: {0}", args.BranchBinLog?.FullName ?? "<not-set>");
            Console.WriteLine("Repository path: {0}", args.Repository.FullName);
            Console.WriteLine("Branch name: {0}", args.Branch ?? "<default>");
            Console.WriteLine("Output JSON file: {0}", args.OutputJson?.FullName ?? "<not-set>");
        }

        var diff = LoadDiff(args.Repository, args.Branch);
        if (diff.changedFiles.Count == 0 && diff.deletedFiles.Count == 0)
        {
            Console.WriteLine("No file has been changed or deleted, quitting.");
            return 0;
        }

        if (args.Debug)
        {
            foreach (var (fullPath, _) in diff.changedFiles)
            {
                Console.Error.WriteLine("Changed file: {0}", fullPath);
            }

            foreach (var (fullPath, _) in diff.deletedFiles)
            {
                Console.Error.WriteLine("Deleted file: {0}", fullPath);
            }
        }

        var binaryLog = LoadBinaryLog(args.BinLog);
        var projectsAffectedByChange = GetAffectedProjects(binaryLog, diff.changedFiles);

        if (args.BranchBinLog != null && diff.deletedFiles.Count > 0)
        {
            var branchBinaryLog = LoadBinaryLog(args.BranchBinLog);

            var deletedFiles = diff.deletedFiles
                .Select(x => x.ChangeRoot(args.Repository, branchBinaryLog.GetRootDirectory()))
                .ToList();

            var projectsAffectedByDeletion = GetAffectedProjects(branchBinaryLog, deletedFiles)
                .ChangeRoot(branchBinaryLog.GetRootDirectory(), args.Repository);

            projectsAffectedByChange = projectsAffectedByChange.MergeWith(projectsAffectedByDeletion);
        }

        var (directlyAffectedProjects, upstreamTree, downstreamTree) = projectsAffectedByChange;

        var output = new ProgramOutput(
            directlyAffectedProjects
                .Union(upstreamTree)
                .Where(i => i.Exists)
                .Select(i => i.FullPath)
                .Distinct().Order().ToList(),
            directlyAffectedProjects
                .Union(upstreamTree)
                .Union(downstreamTree)
                .Where(i => i.Exists)
                .Select(i => i.FullPath)
                .Distinct().Order().ToList());

        var outputJson = JsonSerializer.Serialize(output, _jsonOptions);

        Console.WriteLine(outputJson);

        if (args.OutputJson != null)
        {
            File.WriteAllText(args.OutputJson.FullName!, outputJson);
            Console.WriteLine($"JSON output was written to '{args.OutputJson.FullName!}'.");
        }

        return 0;
    }

    private static AffectedProjects GetAffectedProjects(Build build, IList<FilePath> affectedFiles)
    {
        var (projects, files) = LoadDependencies(build);

        var filesToAffectedProjects =
            affectedFiles.Join(files, i => i.FullPath, j => j.Key, (i, j) => (i.FullPath, j.Value))
            .ToList();

        var directlyAffectedProjects =
            filesToAffectedProjects
            .SelectMany(i => i.Value)
            .ToList();

        var upstreamTree =
            directlyAffectedProjects
            .SelectMany(i => GetUpstreamDependencies(projects, i))
            .Distinct()
            .ToList();

        var downstreamTree =
            upstreamTree.Union(directlyAffectedProjects)
            .SelectMany(i => GetDownstreamDependencies(projects, i))
            .Distinct()
            .ToList();

        return new AffectedProjects(directlyAffectedProjects, upstreamTree, downstreamTree);
    }

    private static (IList<FilePath> changedFiles, IList<FilePath> deletedFiles) LoadDiff(FileSystemInfo repositoryPath, string? remoteBranchName)
    {
        var changes = Git.Diff(repositoryPath.FullName, remoteBranchName)
           .Where(i => !Git.UnchangedStatuses.Contains(i.Status))
           .Select(change => FilePath.Create(change.Path, repositoryPath.FullName))
           .ToList();

        var changedFiles = changes
           .Where(filePath => filePath.Exists)
           .ToList();

        var deletedFiles = changes
           .Where(filePath => !filePath.Exists)
           .ToList();

        return (changedFiles, deletedFiles);
    }

    private static Build LoadBinaryLog(FileSystemInfo binaryLog)
    {
        var build = BinaryLog.ReadBuild(binaryLog.FullName);
        if (!build.Succeeded)
        {
            throw new InvalidOperationException(
                "Build did not finish successfully, can't proceed.");
        }

        return build;
    }

    private static (
        Dictionary<FilePath, HashSet<FilePath>> projects,
        Dictionary<FilePath, HashSet<FilePath>> files)
        LoadDependencies(Build build)
    {
        Dictionary<FilePath, HashSet<FilePath>> projects = new();
        Dictionary<FilePath, HashSet<FilePath>> files = new();

        foreach (var children in build.EvaluationFolder.Children)
        {
            if (children is not ProjectEvaluation project)
            {
                // Ignore types other than project evaluation.
                continue;
            }

            if (project.ProjectFile.EndsWith("metaproj"))
            {
                // Ignore MSBuild's meta project evaluation.
                continue;
            }

            var properties = project.CreateProjectProperties(build.GetRootDirectory());
            var items = project.FindChild<Folder>("Items");

            var projectPath = FilePath.Create(properties.ProjectFullPath);

            // Build project reference map.
            projects.Extend(projectPath, items.ExtractProjectReferences(properties));

            // Build file reference map.
            files.Extend(new[] { projectPath }, projectPath);
            files.Extend(items.ExtractAdditionalFiles(properties), projectPath);
            files.Extend(items.ExtractCompile(properties), projectPath);
            files.Extend(items.ExtractNone(properties), projectPath);
            files.Extend(items.ExtractGlobalAnalyzerConfigFiles(properties), projectPath);
            files.Extend(items.ExtractEditorConfigFiles(properties), projectPath);
            files.Extend(items.ExtractPotentialEditorConfigFiles(properties), projectPath);

            var imports = project.FindChild<TimedNode>("Imports");
            if (imports is null || !imports.HasChildren)
            {
                continue;
            }

            files.Extend(imports.ExtractImports(properties), projectPath);
        }

        return (projects, files);
    }

    private static IEnumerable<FilePath> GetDownstreamDependencies(
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

    private static IEnumerable<FilePath> GetUpstreamDependencies(
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

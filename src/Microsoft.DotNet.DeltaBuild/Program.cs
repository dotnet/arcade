// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.DotNet.DeltaBuild;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
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

        GitDiff diff = LoadDiff(args.Repository, args.Branch);
        if (diff.ChangedFiles.Count == 0 && diff.DeletedFiles.Count == 0)
        {
            Console.WriteLine("No file has been changed or deleted, quitting.");
            return 0;
        }

        if (args.Debug)
        {
            foreach ((IFileSystem fullPath, _) in diff.ChangedFiles)
            {
                Console.Error.WriteLine("Changed file: {0}", fullPath);
            }

            foreach ((IFileSystem fullPath, _) in diff.DeletedFiles)
            {
                Console.Error.WriteLine("Deleted file: {0}", fullPath);
            }
        }

        FileSystem fileSystem = new();
        Build binaryLog = LoadBinaryLog(args.BinLog);
        AffectedProjects affectedProjects = GetAffectedProjects(binaryLog, diff.ChangedFiles);

        if (args.BranchBinLog != null && diff.DeletedFiles.Count > 0)
        {
            Build branchBinaryLog = LoadBinaryLog(args.BranchBinLog);

            List<FilePath> deletedFiles =
                diff.DeletedFiles
                    .Select(x => x.ChangeRoot(args.Repository, branchBinaryLog.GetRootDirectory()))
                    .ToList();

            AffectedProjects projectsAffectedByDeletion =
                GetAffectedProjects(branchBinaryLog, deletedFiles)
                    .ChangeRoot(fileSystem, branchBinaryLog.GetRootDirectory(), args.Repository);

            affectedProjects = affectedProjects.MergeWith(projectsAffectedByDeletion);
        }

        ProgramOutput output = new(
            affectedProjects.DirectlyAffectedProjects
                .Union(affectedProjects.UpstreamTree)
                .Where(i => i.Exists)
                .Select(i => i.FullPath)
                .Distinct().Order().ToList(),
            affectedProjects.DirectlyAffectedProjects
                .Union(affectedProjects.UpstreamTree)
                .Union(affectedProjects.DownstreamTree)
                .Where(i => i.Exists)
                .Select(i => i.FullPath)
                .Distinct().Order().ToList());

        string outputJson = JsonSerializer.Serialize(output, JsonOptions);

        Console.WriteLine(outputJson);

        if (args.OutputJson != null)
        {
            File.WriteAllText(args.OutputJson.FullName, outputJson);
            Console.WriteLine($"JSON output was written to '{args.OutputJson.FullName}'.");
        }

        return 0;
    }

    private static AffectedProjects GetAffectedProjects(Build build, IList<FilePath> affectedFiles)
    {
        ProjectDependencies dependencies = LoadDependencies(build);

        List<(string FullPath, HashSet<FilePath> Value)> filesToAffectedProjects =
            affectedFiles
                .Join(dependencies.Files, i => i.FullPath, j => j.Key, (i, j) => (i.FullPath, j.Value))
                .ToList();

        List<FilePath> directlyAffectedProjects =
            filesToAffectedProjects
                .SelectMany(i => i.Value)
                .ToList();

        List<FilePath> upstreamTree =
            directlyAffectedProjects
                .SelectMany(i => DependencyResolver.GetUpstreamDependencies(dependencies.Projects, i))
                .Distinct()
                .ToList();

        List<FilePath> downstreamTree =
            upstreamTree.Union(directlyAffectedProjects)
                .SelectMany(i => DependencyResolver.GetDownstreamDependencies(dependencies.Projects, i))
                .Distinct()
                .ToList();

        return new AffectedProjects(directlyAffectedProjects, upstreamTree, downstreamTree);
    }

    private static GitDiff LoadDiff(FileSystemInfo repositoryPath, string? remoteBranchName)
    {
        List<FilePath> changes =
            Git.Diff(repositoryPath.FullName, remoteBranchName)
               .Where(i => !Git.UnchangedStatuses.Contains(i.Status))
               .Select(change => FilePath.Create(change.Path, repositoryPath.FullName))
               .ToList();

        List<FilePath> changedFiles =
            changes
               .Where(filePath => filePath.Exists)
               .ToList();

        List<FilePath> deletedFiles =
            changes
               .Where(filePath => !filePath.Exists)
               .ToList();

        return new GitDiff(deletedFiles, changedFiles);
    }

    private static Build LoadBinaryLog(FileSystemInfo binaryLog)
    {
        Build? build = BinaryLog.ReadBuild(binaryLog.FullName);
        if (!build.Succeeded)
        {
            throw new InvalidOperationException(
                "Build did not finish successfully, can't proceed.");
        }

        return build;
    }

    private static ProjectDependencies LoadDependencies(Build build)
    {
        Dictionary<FilePath, HashSet<FilePath>> projects = new();
        Dictionary<FilePath, HashSet<FilePath>> files = new();

        foreach (BaseNode? children in build.EvaluationFolder.Children)
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

            TimedNode? imports = project.FindChild<TimedNode>("Imports");
            if (imports is null || !imports.HasChildren)
            {
                continue;
            }

            files.Extend(imports.ExtractImports(properties), projectPath);
        }

        return new ProjectDependencies(projects, files);
    }
}

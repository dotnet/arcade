// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeltaBuild;

public record ProgramArgs(
    FileInfo? BranchBinLog,
    FileInfo BinLog,
    DirectoryInfo Repository,
    string? Branch,
    FileInfo? OutputJson,
    bool Debug)
{
    public static Task<int> ParseAndRunAsync(Func<ProgramArgs, int> run, string[] args)
    {
        var command = new RootCommand("Provides list of projects affected by a Git diff.")
        {
            // Option: --branch-binlog
            new Option<FileInfo>(
                new[] { "--branch-binlog", "-bbl" },
                "Path to a MSBuild binary log created from the target branch " +
                "(specified by --branch parameter)."),

            // Option: --binlog
            new Option<FileInfo>(
                new[] { "--binlog", "-bl" },
                "Path to a MSBuild binary log created from the current branch.")
                { IsRequired = true },

            // Option: --repository
            new Option<DirectoryInfo>(
                new[] { "--repository", "-r" },
                "Path to the root of Git repository.")
                { IsRequired = true },

            // Option: --output-json
            new Option<FileInfo>(
                new[] { "--output-json", "-o" },
                "Name of the output JSON file. It contains 2 lists of projects."
                + "'AffectedProjectChain' is a list of projects affected by the changes."
                + "'AffectedProjects' is a list of projects affected by the changes " +
                "together with their downstream dependencies."),

            // Option: --branch
            new Option<string>(
                new[] { "--branch", "-b" },
                "The name of the target branch (local or remote) that the " +
                "current changes will be merged into. Default: origin/main"),

            // Option: --debug
            new Option<bool>(new[] { "--debug", "-d" }, "Turn on debugging")
        };

        command.Handler = CommandHandler.Create(run);
        return command.InvokeAsync(args);
    }
}

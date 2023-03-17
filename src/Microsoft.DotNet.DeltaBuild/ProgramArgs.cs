// Copyright (c) Microsoft Corporation. All Rights Reserved.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Abstractions;

namespace Microsoft.DotNet.DeltaBuild;

internal record ProgramArgs(
    FileInfo? BranchBinLog,
    FileInfo BinLog,
    DirectoryInfo Repository,
    string? Branch,
    FileInfo? OutputJson,
    bool Debug)
{
    public static Task<int> ParseAndRunAsync(Func<ProgramArgs, int> run, string[] args)
    {
            var app = new CommandLineApplication
            {
                Name = "DeltaBuild",
                Description = "Provides list of projects affected by a Git diff.",
            };
            
            app.ValueParsers.Add(new FileInfoParser());
            app.ValueParsers.Add(new DirectoryInfoParser());

            app.ExtendedHelpText = @"
Output json file format contains two lists. AffectedProjectChain lists all projects directly or transiently affected
by the changes. AffectedProjects is an extended list also contains all the downstream dependencies so it can be built. 
";
            
            app.HelpOption(inherited: true);
            
            var branchBinaryLog = app.Option<FileInfo>(
                "--branch-binlog|-bbl", 
                "Path to a MSBuild binary log created from the target branch (specified by --branch parameter).", 
                CommandOptionType.SingleValue);

            var binaryLog = app.Option<FileInfo>(
                "--binlog|-bl", 
                "Path to a MSBuild binary log created from the current branch.", 
                CommandOptionType.SingleValue)
                .IsRequired();

            var repositoryPath = app.Option<DirectoryInfo>(
                "--repository|-r", 
                "Path to the root of Git repository.", 
                CommandOptionType.SingleValue)
                .IsRequired();

            var branch = app.Option<string>(
                "--branch|-b", 
                "Name of the branch, local or remote, that changes will be merged into. Default: origin/main", 
                CommandOptionType.SingleValue);

            var outputJson = app.Option<FileInfo>(
                "--output-json|-o", 
                "Name of the output JSON file. See details below.",
                CommandOptionType.SingleValue);

            var debug = app.Option<bool>(
                "--debug|-d", "Turn on debugging", 
                CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                var programArgs = new ProgramArgs(
                    branchBinaryLog.HasValue() ? branchBinaryLog.ParsedValue : null,
                    binaryLog.ParsedValue,
                    repositoryPath.ParsedValue,
                    branch.HasValue() ? branch.ParsedValue : null,
                    outputJson.HasValue() ? outputJson.ParsedValue : null,
                    debug.HasValue());

                return run(programArgs);
            });

            return Task.FromResult(app.Execute(args));
    }
    
    private class FileInfoParser : IValueParser
    {
        public Type TargetType { get; } = typeof(FileInfo);
        public object Parse(string argName, string value, CultureInfo culture)
        {
            return new FileInfo(value);
        }
    }

    private class DirectoryInfoParser : IValueParser
    {
        public Type TargetType { get; } = typeof(DirectoryInfo);
        public object Parse(string argName, string value, CultureInfo culture)
        {
            return new DirectoryInfo(value);
        }
    }
}

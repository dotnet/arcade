// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using Microsoft.DotNet.VersionTools.Automation;

namespace Microsoft.DotNet.VersionTools.Cli;

class Program
{
    static int Main(string[] args)
    {
        //// Global options

        CliRootCommand rootCommand = new("Microsoft.DotNet.VersionTools.Cli v" + Environment.Version.ToString(2))
        {
            TreatUnmatchedTokensAsErrors = true
        };

        // Package command
        CliOption<string> assetsDirectoryOption = new("--assets-path", "-p")
        {
            Description = "Path to the directory where the nuget assets are located",
            Required = true
        };

        CliOption<string> searchPatternOption = new("--search-pattern", "-s")
        {
            Description = "The search string to match against the names of subdirectories in --assets-path. See Directory.GetFiles for details.",
            DefaultValueFactory = _ => "*.nupkg"
        };

        CliOption<bool> recursiveOption = new("--recursive", "-r")
        {
            Description = "Search for nuget assets recursively.",
            DefaultValueFactory = _ => true
        };

        CliCommand trimAssetVersion = new("trim-assets-version", "Trim the version for given nuget assets.");
        trimAssetVersion.Options.Add(assetsDirectoryOption);
        trimAssetVersion.Options.Add(searchPatternOption);
        trimAssetVersion.Options.Add(recursiveOption);
        trimAssetVersion.SetAction(result =>
        {
            var operation = new VersionTrimmingOperation(
                new VersionTrimmingOperation.Context
                {
                    NupkgInfoFactory = new NupkgInfoFactory(new PackageArchiveReaderFactory()),
                    DirectoryProxy = new DirectoryProxy(),
                    FileProxy = new FileProxy(),

                    AssetsDirectory = result.GetValue(assetsDirectoryOption),
                    SearchPattern = result.GetValue(searchPatternOption),
                    Recursive = result.GetValue(recursiveOption)
                });
            return (int)operation.Execute();
        });

        rootCommand.Subcommands.Add(trimAssetVersion);
        return new CliConfiguration(rootCommand).Invoke(args);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.VersionTools.Automation;

namespace Microsoft.DotNet.VersionTools.Cli;

class Program
{
    static int Main(string[] args)
    {
        //// Global options

        RootCommand rootCommand = new("Microsoft.DotNet.VersionTools.Cli v" + Environment.Version.ToString(2))
        {
            TreatUnmatchedTokensAsErrors = true
        };

        // Package command
        Option<string> assetsDirectoryOption = new("--assets-path",
            "Path to the directory where the nuget assets are located")
        {
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true,
        };

        Option<string> searchPatternOption = new("--search-pattern",
            "The search string to match against the names of subdirectories in --assets-path. See Directory.GetFiles for details.")
        {
            Arity = ArgumentArity.ExactlyOne,
        };
        searchPatternOption.SetDefaultValue("*.nupkg");

        Option<bool> recursiveOption = new("--recursive",
            "Search for nuget assets recursively.");
        recursiveOption.SetDefaultValue(true);

        Command trimAssetVersion = new("trim-assets-version", "Trim the version for given nuget assets.");
        trimAssetVersion.AddOption(assetsDirectoryOption);
        trimAssetVersion.AddOption(searchPatternOption);
        trimAssetVersion.AddOption(recursiveOption);
        trimAssetVersion.SetHandler((InvocationContext context) =>
        {
            var operation = new VersionTrimmingOperation(
                new VersionTrimmingOperation.Context
                {
                    NupkgInfoFactory = new NupkgInfoFactory(new PackageArchiveReaderFactory()),
                    DirectoryProxy = new DirectoryProxy(),
                    FileProxy = new FileProxy(),

                    AssetsDirectory = context.ParseResult.GetValueForOption(assetsDirectoryOption),
                    SearchPattern = context.ParseResult.GetValueForOption(searchPatternOption),
                    Recursive = context.ParseResult.GetValueForOption(recursiveOption)
                });

            context.ExitCode = (int)operation.Execute();
        });

        rootCommand.AddCommand(trimAssetVersion);
        return rootCommand.Invoke(args);
    }
}

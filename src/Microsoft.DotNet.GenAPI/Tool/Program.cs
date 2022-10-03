// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.GenAPI.Shared;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.GenAPI.Tool;


/// <summary>
/// CLI frontend for the Roslyn-based GenAPI.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        // Global options
        Option<string> assemblyOption = new("--assembly",
            "Delimited (',' or ';') set of paths for assemblies or directories to get all assemblies.")
        {
            ArgumentHelpName = "file,dir,...",
            IsRequired = true
        };

        Option<bool> resolveAssemblyReferencesOption = new("--resolve-assembly-references",
            "Default is `false`. If true, tries to resolve assembly reference.");

        Option<string?> libPathOption = new("--lib-path",
            "Delimited (',' or ';') set of paths to use for resolving assembly references.");


        Option<string?> outputPathOption = new("--output-path",
            @"Output path. Default is the console. Can specify an existing directory as well
            and then a file will be created for each assembly with the matching name of the assembly.");

        Option<string?> headerFileOption = new("--header-file",
            "Specify a file with an alternate header content to prepend to output.");

        Option<string?> exceptionMessageOption = new("--exception-message",
            "If specified - method bodies should throw PlatformNotSupportedException, else `throw null`.");

        RootCommand rootCommand = new("Microsoft.DotNet.GenAPI")
        {
            TreatUnmatchedTokensAsErrors = true
        };
        rootCommand.AddGlobalOption(assemblyOption);
        rootCommand.AddGlobalOption(resolveAssemblyReferencesOption);
        rootCommand.AddGlobalOption(libPathOption);
        rootCommand.AddGlobalOption(outputPathOption);
        rootCommand.AddGlobalOption(headerFileOption);
        rootCommand.AddGlobalOption(exceptionMessageOption);

        rootCommand.SetHandler((InvocationContext context) =>
        {
            var assemblyPathes = context.ParseResult.GetValueForOption(assemblyOption);
            var libPathes = context.ParseResult.GetValueForOption(libPathOption);
            var exceptionMessage = context.ParseResult.GetValueForOption(exceptionMessageOption);
            var headerFile = context.ParseResult.GetValueForOption(headerFileOption);
            var outputDirPath = context.ParseResult.GetValueForOption(outputPathOption);
            var resolveAssemblyReferences = context.ParseResult.GetValueForOption(resolveAssemblyReferencesOption);

            var loader = new AssemblySymbolLoader(resolveAssemblyReferences);
            loader.AddReferenceSearchDirectories(Utils.SplitPaths(libPathes));

            var assemblySymbols = loader.LoadAssemblies(Utils.SplitPaths(assemblyPathes));
            foreach (var assemblySymbol in assemblySymbols)
            {
                using var writer = Utils.GetCSharpBuilder(
                    assemblySymbol.Name,
                    outputDirPath,
                    headerFile,
                    exceptionMessage);

                writer.WriteAssembly(assemblySymbol);
            }

            foreach (var warn in loader.GetResolutionWarnings())
            {
                Console.WriteLine(warn);
            }
        });

        return rootCommand.Invoke(args);
    }
}

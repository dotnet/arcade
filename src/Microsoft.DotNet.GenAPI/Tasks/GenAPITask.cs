// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks;
using Microsoft.DotNet.GenAPI.Shared;

namespace Microsoft.DotNet.GenAPI.Tasks;

#nullable enable

/// <summary>
/// MSBuild task frontend for the Roslyn-based GenAPI.
/// </summary>
public class GenAPITask : BuildTask
{
    /// <summary>
    /// Delimited (',' or ';') set of paths for assemblies or directories to get all assemblies.
    /// </summary>
    [Required]
    public string? Assembly { get; set; }

    /// <summary>
    /// If true, tries to resolve assembly reference.
    /// </summary>
    public bool? ResolveAssemblyReferences { get; set; }
    
    /// <summary>
    /// Delimited (',' or ';') set of paths to use for resolving assembly references.
    /// </summary>
    public string? LibPath { get; set; }

    /// <summary>
    /// Output path. Default is the console. Can specify an existing directory as well and
    /// then a file will be created for each assembly with the matching name of the assembly.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Specify a file with an alternate header content to prepend to output.
    /// </summary>
    public string? HeaderFile { get; set; }

    /// <summary>
    /// Method bodies should throw PlatformNotSupportedException.
    /// </summary>
    public string? ExceptionMessage { get; set; }

    public override bool Execute()
    {
        var loader = new AssemblySymbolLoader(ResolveAssemblyReferences ?? false);
        loader.AddReferenceSearchDirectories(Utils.SplitPaths(LibPath));

        var assemblySymbols = loader.LoadAssemblies(Utils.SplitPaths(Assembly));
        foreach (var assemblySymbol in assemblySymbols)
        {
            using var writer = Utils.GetCSharpBuilder(
                assemblySymbol.Name,
                OutputPath,
                HeaderFile,
                ExceptionMessage);

            writer.WriteAssembly(assemblySymbol);
        }

        foreach (var warn in loader.GetResolutionWarnings())
        {
            Console.WriteLine(warn);
        }

        return true;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.GenAPI.Shared;


/// <summary>
/// Class to standertize initilization and running of GenAPI tool.
///     Shared between CLI and MSBuild tasks frontends.
/// </summary>
public static class GenAPIApp
{
    public class Context
    {
        /// <summary>
        /// Delimited (',' or ';') set of paths for assemblies or directories to get all assemblies.
        /// </summary>
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
    }

    /// <summary>
    /// Initialize and run Roslyn-based GenAPI tool.
    /// </summary>
    public static void Run(Context cntx)
    {
        var loader = new AssemblySymbolLoader(cntx.ResolveAssemblyReferences ?? false);
        loader.AddReferenceSearchDirectories(SplitPaths(cntx.LibPath));

        var assemblySymbols = loader.LoadAssemblies(SplitPaths(cntx.Assembly));
        foreach (var assemblySymbol in assemblySymbols)
        {
            using var writer = GetCSharpBuilder(
                assemblySymbol.Name,
                cntx.OutputPath,
                cntx.HeaderFile,
                cntx.ExceptionMessage);

            writer.WriteAssembly(assemblySymbol);
        }

        foreach (var warn in loader.GetResolutionWarnings())
        {
            Console.WriteLine(warn);
        }
    }

    /// <summary>
    /// Creates a TextWriter capable to write into Console or cs file.
    /// </summary>
    /// <param name="outputDirPath">Path to a directory where file with `assemblyName`.cs filename needs to be created.
    ///     If Null - output to Console.Out.</param>
    /// <param name="assemblyName">Name of an assembly. if outputDirPath is not a Null - represents a file name.</param>
    /// <returns></returns>
    private static TextWriter GetTextWriter(string? outputDirPath, string assemblyName)
    {
        if (outputDirPath == null)
        {
            return Console.Out;
        }

        string fileName = assemblyName + ".cs";
        if (Directory.Exists(outputDirPath) && !string.IsNullOrEmpty(fileName))
        {
            return File.CreateText(Path.Combine(outputDirPath, fileName));
        }

        return File.CreateText(outputDirPath);
    }

    /// <summary>
    /// Creates a CSharpBuilder object based on input parameters.
    /// </summary>
    /// <param name="assemblyName">Delimited (',' or ';') set of paths for assemblies or
    ///     directories to get all assemblies.</param>
    /// <param name="outputPath">Default is the console. Can specify an existing directory as well
    ///     and then a file will be created for each assembly with the matching name of the assembly.</param>
    /// <param name="headerFile">Specify a file with an alternate header content to prepend to output.</param>
    /// <param name="exceptionMessage">If specified - method bodies should throw PlatformNotSupportedException,
    ///     else `throw null`.</param>
    /// <returns></returns>
    private static CSharpBuilder GetCSharpBuilder(
        string assemblyName,
        string? outputPath,
        string? headerFile,
        string? exceptionMessage)
    {
        return new CSharpBuilder(
            new AssemblySymbolOrderProvider(),
            new IncludeAllFilter(),
            new CSharpSyntaxWriter(
                GetTextWriter(outputPath, assemblyName),
                ReadHeaderFile(headerFile),
                exceptionMessage));
    }

    /// <summary>
    /// Splits delimiter separated list of pathes represented as a string to a List of paths.
    /// </summary>
    /// <param name="pathSet">Delimiter separated list of paths.</param>
    /// <returns></returns>
    private static string[] SplitPaths(string? pathSet)
    {
        if (pathSet == null) return Array.Empty<string>();

        return pathSet.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Read the header file if specified, or use default one.
    /// </summary>
    /// <param name="headerFile">File with an alternate header content to prepend to output</param>
    /// <returns></returns>
    public static string ReadHeaderFile(string? headerFile)
    {
        const string defaultFileHeader = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //     Roslyn-based GenAPI {0}
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            """;

        if (!string.IsNullOrEmpty(headerFile))
        {
            return File.ReadAllText(headerFile);
        }
        var version = typeof(GenAPIApp).Assembly.GetName().Version;
        return version != null ?
            string.Format(defaultFileHeader, "Version: " + version.ToString()) :
            defaultFileHeader;
    }
}

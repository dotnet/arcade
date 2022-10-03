// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Utility functions shared between CLI and MSBuild tasks frontends.
/// </summary>
public static class Utils
{
    /// <summary>
    /// Creates a TextWriter capable to write into Console or cs file.
    /// </summary>
    /// <param name="outputDirPath">Path to a directory where file with `assemblyName`.cs filename needs to be created.
    ///     If Null - output to Console.Out.</param>
    /// <param name="assemblyName">Name of an assembly. if outputDirPath is not a Null - represents a file name.</param>
    /// <returns></returns>
    public static TextWriter GetTextWriter(string? outputDirPath, string assemblyName)
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
    public static CSharpBuilder GetCSharpBuilder(
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
                FileHeader.ReadFromFile(headerFile),
                exceptionMessage));
    }

    /// <summary>
    /// Splits delimiter separated list of pathes represented as a string to a List of paths.
    /// </summary>
    /// <param name="pathSet">Delimiter separated list of paths.</param>
    /// <returns></returns>
    public static string[] SplitPaths(string? pathSet)
    {
        if (pathSet == null) return Array.Empty<string>();

        return pathSet.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }
}

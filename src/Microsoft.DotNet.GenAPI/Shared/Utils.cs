// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Utility functions shared between CLI and MSBuild tasks frontends.
/// </summary>
public class Utils
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
    /// Splits delimeter seperated list of pathes represented as a string to a List of pathes.
    /// </summary>
    /// <param name="pathSet">Delimeter seperated list of pathes.</param>
    /// <returns></returns>
    public static string[] SplitPaths(string? pathSet)
    {
        if (pathSet == null) return new string[0];

        return pathSet.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }
}

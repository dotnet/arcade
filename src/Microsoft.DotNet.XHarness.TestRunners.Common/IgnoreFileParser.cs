// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Class that can parse a file/stream with the ignored tests and will
/// return a list of the ignored tests.
/// </summary>
internal static class IgnoreFileParser
{
    private static string ParseLine(string line)
    {
        // we have to make sure of several things, first, lets
        // remove any char after the first # which would mean
        // we have comments:
        var pos = line.IndexOf('#');
        if (pos > -1)
        {
            line = line.Remove(pos);
        }
        line = line.Trim();
        return line;
    }
    public static async Task<IEnumerable<string>> ParseStreamAsync(TextReader textReader)
    {
        var ignoredMethods = new List<string>();
        string line;
        while ((line = await textReader.ReadLineAsync()) != null)
        {
            line = ParseLine(line);
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            ignoredMethods.Add(line);
        }
        return ignoredMethods;
    }

    public static async Task<IEnumerable<string>> ParseAssemblyResourcesAsync(Assembly asm)
    {
        var ignoredTests = new List<string>();
        // the project generator added the required resources,
        // we extract them, parse them and add the result
        foreach (var resourceName in asm.GetManifestResourceNames())
        {
            if (resourceName.EndsWith(".ignore", StringComparison.Ordinal))
            {
                using (var stream = asm.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    var ignored = await ParseStreamAsync(reader);
                    // we could have more than one file, lets add them
                    ignoredTests.AddRange(ignored);
                }
            }
        }
        return ignoredTests;
    }

    public static async Task<IEnumerable<string>> ParseContentFilesAsync(string contentDir)
    {
        if (string.IsNullOrEmpty(contentDir))
        {
            return Array.Empty<string>();
        }

        var ignoredTests = new List<string>();
        foreach (var f in Directory.GetFiles(contentDir, "*.ignore"))
        {
            using (var reader = new StreamReader(f))
            {
                var ignored = await ParseStreamAsync(reader);
                ignoredTests.AddRange(ignored);
            }
        }
        return ignoredTests;
    }

    public static async Task<IEnumerable<string>> ParseTraitsFileAsync(string filePath)
    {
        var ignoredTraits = new List<string>();
        using var reader = new StreamReader(filePath);
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            ignoredTraits.Add(line);
        }
        return ignoredTraits;
    }

    public static Task<IEnumerable<string>> ParseTraitsContentFileAsync(string contentDir, bool isXUnit)
    {
        var ignoreFile = Path.Combine(contentDir, isXUnit ? "xunit-excludes.txt" : "nunit-excludes.txt");
        return ParseTraitsFileAsync(ignoreFile);
    }

    public static IEnumerable<string> ParseTraitsContentFile(string contentDir, bool isXUnit)
    {
        var ignoredTraits = new List<string>();
        var ignoreFile = Path.Combine(contentDir, isXUnit ? "xunit-excludes.txt" : "nunit-excludes.txt");
        using (var reader = new StreamReader(ignoreFile))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                ignoredTraits.Add(line);
            }
        }
        return ignoredTraits;
    }

    public static IEnumerable<string> ParseContentFiles(string contentDir)
    {
        var ignoredTests = new List<string>();
        foreach (var f in Directory.GetFiles(contentDir, "*.ignore"))
        {
            using (var reader = new StreamReader(f))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {

                    line = ParseLine(line);
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    ignoredTests.Add(line);
                }
            }
        }
        return ignoredTests;
    }
}

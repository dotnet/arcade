// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

#nullable enable

namespace Microsoft.DotNet.Pkg
{
    public static class Processor
    {
        internal static string WorkingDirectory = Directory.GetCurrentDirectory();
        internal static string InputPath = string.Empty;
        internal static string OutputPath = string.Empty;

        public static void Initialize(string inputPath, string outputPath = "")
        {
            InputPath = inputPath;
            OutputPath = outputPath;
        }

        public static void Unpack()
        {
            if (!File.Exists(InputPath))
            {
                throw new Exception("Input path must be a valid file");
            }

            if (!IsPkg(InputPath) && !IsAppBundle(InputPath))
            {
                throw new Exception("Input path must be a .pkg or .app (zipped) file");
            }

            if (!Directory.Exists(OutputPath))
            {
                Directory.CreateDirectory(OutputPath);
            }

            if (IsPkg(InputPath))
            {
                Package.Unpack();
            }
            else if (IsAppBundle(InputPath))
            {
                AppBundle.Unpack(InputPath, OutputPath);
            }
        }
        
        public static void Repack()
        {
            if (!Directory.Exists(InputPath))
            {
                throw new Exception("Input path must be a valid directory");
            }

            if (!IsPkg(OutputPath) && !IsAppBundle(OutputPath))
            {
                throw new Exception("Output path must be a .pkg or .app (zipped) file");
            }

            if (IsPkg(OutputPath))
            {
                Package.Repack();
            }
            else if (IsAppBundle(OutputPath))
            {
                AppBundle.Repack(InputPath, OutputPath);
            }
        }

        internal static bool IsPkg(string path) =>
            Path.GetExtension(path).Equals(".pkg");

        internal static bool IsAppBundle(string path) =>
            Path.GetExtension(path).Equals(".app");

        internal static string? FindInPath(string name, string path, bool isDirectory, SearchOption searchOption = SearchOption.AllDirectories)
        {
            try
            {
                List<string> results = isDirectory ? GetDirectories(path, name, searchOption).ToList() : GetFiles(path, name, searchOption).ToList();
                if (results.Count == 1)
                {
                    return results[0];
                }
                else if (results.Count > 1)
                {
                    throw new Exception($"Multiple files found with name '{name}'");
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error finding file '{name}' in '{path}': {e.Message}");
            }
        }

        internal static IEnumerable<string> GetDirectories(string path, string pathFilter = "*", SearchOption searchOption = SearchOption.AllDirectories) =>
            Directory.EnumerateDirectories(path, pathFilter, searchOption);

        internal static IEnumerable<string> GetFiles(string path, string pathFilter = "*", SearchOption searchOption = SearchOption.AllDirectories) =>
            Directory.EnumerateFiles(path, pathFilter, searchOption);
    }
}

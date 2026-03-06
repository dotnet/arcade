// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    internal static class RootInputOutputPathHelper
    {
        public static string GetCommonRootForFiles(IReadOnlyList<string> inputFilePaths)
        {
            if (inputFilePaths == null || inputFilePaths.Count == 0)
            {
                throw new ArgumentException("At least one input file path is required.", nameof(inputFilePaths));
            }

            var directories = inputFilePaths
                .Select(path => NormalizeDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetFullPath(path)))
                .ToArray();

            string common = directories[0];
            foreach (string directory in directories.Skip(1))
            {
                common = GetCommonDirectoryPrefix(common, directory);
            }

            return common;
        }

        public static string BuildOutputPath(string sourcePath, string outputDirectory, string commonRoot)
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            string relativePath = Path.GetRelativePath(commonRoot, fullSourcePath);
            return Path.Combine(outputDirectory, relativePath);
        }

        private static string GetCommonDirectoryPrefix(string first, string second)
        {
            string firstRoot = Path.GetPathRoot(first) ?? string.Empty;
            string secondRoot = Path.GetPathRoot(second) ?? string.Empty;
            if (!string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase))
            {
                return firstRoot;
            }

            string[] firstSegments = GetSegments(first, firstRoot);
            string[] secondSegments = GetSegments(second, secondRoot);
            int count = Math.Min(firstSegments.Length, secondSegments.Length);

            int sharedCount = 0;
            while (sharedCount < count &&
                   string.Equals(firstSegments[sharedCount], secondSegments[sharedCount], StringComparison.OrdinalIgnoreCase))
            {
                sharedCount++;
            }

            if (sharedCount == 0)
            {
                return EnsureDirectoryPath(firstRoot);
            }

            string prefix = Path.Combine(firstRoot, Path.Combine(firstSegments.Take(sharedCount).ToArray()));
            return EnsureDirectoryPath(prefix);
        }

        private static string[] GetSegments(string path, string root)
        {
            string remainder = path.Substring(root.Length)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.IsNullOrEmpty(remainder))
            {
                return Array.Empty<string>();
            }

            return remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string NormalizeDirectory(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return EnsureDirectoryPath(fullPath);
        }

        private static string EnsureDirectoryPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (normalized.EndsWith(Path.DirectorySeparatorChar))
            {
                return normalized;
            }

            return normalized + Path.DirectorySeparatorChar;
        }
    }
}

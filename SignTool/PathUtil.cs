// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SignTool
{
    internal static class PathUtil
    {
        internal static bool IsVsix(string fileName) => Path.GetExtension(fileName) == ".vsix";

        internal static bool IsAssembly(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return ext == ".exe" || ext == ".dll";
        }

        internal static bool IsAnyDirectorySeparator(char c) => c == '\\' || c == '/';

        internal static List<string> ExpandDirectoryGlob(string globPath)
        {
            var all = ExpandDirectoryGlobCore(globPath);
            return all
                .Where(File.Exists)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }

        internal static IEnumerable<string> ExpandDirectoryGlobCore(string globPath)
        {
            var firstStarIndex = globPath.IndexOf('*');
            if (firstStarIndex <= 2 || 
                firstStarIndex + 1 == globPath.Length ||
                !IsAnyDirectorySeparator(globPath[firstStarIndex - 1]))
            {
                throw CreateBadGlobException();
            }

            var baseDir = globPath.Substring(0, length: firstStarIndex - 1);

            var nextCharIndex = firstStarIndex + 1;
            var nextChar = globPath[nextCharIndex];
            if (IsAnyDirectorySeparator(nextChar))
            {
                if (nextCharIndex + 1 >= globPath.Length)
                {
                    throw CreateBadGlobException();
                }

                var childPath = globPath.Substring(nextCharIndex + 1);
                return ExpandGlobDirectorySingle(baseDir, childPath);
            }

            throw CreateBadGlobException();
        }

        private static IEnumerable<string> ExpandGlobDirectorySingle(string baseDir, string childPath)
        {
            foreach (var dir in Directory.EnumerateDirectories(baseDir))
            {
                var fullPath = Path.Combine(dir, childPath);
                if (fullPath.IndexOf('*') > 0)
                {
                    foreach (var expandedPath in ExpandDirectoryGlobCore(fullPath))
                    {
                        yield return expandedPath;
                    }
                }
                else
                {
                    yield return fullPath;
                }
            }
        }

        private static Exception CreateBadGlobException() => new Exception("Globbing is only supported as a * directory names in their entirety");
    }
}

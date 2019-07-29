// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Copied from https://github.com/dotnet/core-setup/blob/b73c0af268be449db317a7c0718012027cd8b173/tools-local/tasks/FileUtilities.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet.Build.Tasks
{
    internal static partial class FileUtilities
    {
        private static readonly HashSet<string> s_assemblyExtensions = new HashSet<string>(
            new[] { ".dll", ".exe", ".winmd" },
            StringComparer.OrdinalIgnoreCase);

        public static Version GetFileVersion(string sourcePath)
        {
            var fvi = FileVersionInfo.GetVersionInfo(sourcePath);

            if (fvi != null)
            {
                return new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart);
            }

            return null;
        }

        public static AssemblyName GetAssemblyName(string path)
        {
            if (!s_assemblyExtensions.Contains(Path.GetExtension(path)))
            {
                return null;
            }

            try
            {
                return AssemblyName.GetAssemblyName(path);
            }
            catch (BadImageFormatException)
            {
                // Not a valid assembly.
                return null;
            }
        }
    }
}
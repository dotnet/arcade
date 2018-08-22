// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct FileSignInfo
    {
        internal readonly string Name;
        internal readonly string FullPath;
        internal readonly SignInfo SignInfo;

        // optional file information that allows to disambiguate among multiple files with the same name:
        internal readonly string TargetFramework;

        internal static bool IsPEFile(string fileFullPath)
        {
            return !string.IsNullOrWhiteSpace(fileFullPath) && (Path.GetExtension(fileFullPath) == ".exe" || Path.GetExtension(fileFullPath) == ".dll");
        }

        internal static bool IsVsix(string fileFullPath)
        {
            return !string.IsNullOrWhiteSpace(fileFullPath) && Path.GetExtension(fileFullPath).Equals(".vsix", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNupkg(string fileFullPath)
        {
            return !string.IsNullOrWhiteSpace(fileFullPath) && Path.GetExtension(fileFullPath).Equals(".nupkg", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsZipContainer(string fileFullPath)
        {
            return IsVsix(fileFullPath) || IsNupkg(fileFullPath);
        }

        internal bool IsPEFile() => IsPEFile(Name);

        internal bool IsVsix() => IsVsix(Name);

        internal bool IsNupkg() => IsNupkg(Name);

        internal bool IsZipContainer() => IsZipContainer(Name);

        internal FileSignInfo(string fullPath, SignInfo signInfo, string targetFramework = null)
        {
            Debug.Assert(fullPath != null);
            Debug.Assert(targetFramework != "");

            Name = Path.GetFileName(fullPath);
            FullPath = fullPath;
            SignInfo = signInfo;
            TargetFramework = targetFramework;
        }
    }
}

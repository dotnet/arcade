// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct FileSignInfo
    {
        internal readonly string FileName;
        internal readonly string FullPath;
        internal readonly SignInfo SignInfo;
        internal readonly ImmutableArray<byte> ContentHash;

        // optional file information that allows to disambiguate among multiple files with the same name:
        internal readonly string TargetFramework;

        internal static bool IsPEFile(string path)
            => Path.GetExtension(path) == ".exe" || Path.GetExtension(path) == ".dll";

        internal static bool IsVsix(string path)
            => Path.GetExtension(path).Equals(".vsix", StringComparison.OrdinalIgnoreCase);
        
        internal static bool IsMPack(string path)
            => Path.GetExtension(path).Equals(".mpack", StringComparison.OrdinalIgnoreCase);

        internal static bool IsNupkg(string path)
            => Path.GetExtension(path).Equals(".nupkg", StringComparison.OrdinalIgnoreCase);

        internal static bool IsZip(string path)
            => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

        internal static bool IsPowerShellScript(string path)
            => Path.GetExtension(path).Equals(".ps1", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".psd1", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".psm1", StringComparison.OrdinalIgnoreCase);

        internal static bool IsPackage(string path)
            => IsVsix(path) || IsNupkg(path);

        internal static bool IsZipContainer(string path)
            => IsPackage(path) || IsMPack(path) || IsZip(path);

        internal bool IsPEFile() => IsPEFile(FileName);

        internal bool IsManaged() => ContentUtil.IsManaged(FullPath);

        internal bool IsCrossgened() => ContentUtil.IsCrossgened(FullPath);

        internal bool IsVsix() => IsVsix(FileName);

        internal bool IsNupkg() => IsNupkg(FileName);

        internal bool IsZip() => IsZip(FileName);

        internal bool IsZipContainer() => IsZipContainer(FileName);

        internal bool IsPackage() => IsPackage(FileName);

        internal bool IsPowerShellScript() => IsPowerShellScript(FileName);

        internal FileSignInfo(string fullPath, ImmutableArray<byte> contentHash, SignInfo signInfo, string targetFramework = null)
        {
            Debug.Assert(fullPath != null);
            Debug.Assert(!contentHash.IsDefault && contentHash.Length == 256 / 8);
            Debug.Assert(targetFramework != "");

            FileName = Path.GetFileName(fullPath);
            ContentHash = contentHash;
            FullPath = fullPath;
            SignInfo = signInfo;
            TargetFramework = targetFramework;
        }

        public override string ToString()
            => $"File '{FileName}'" +
               (TargetFramework != null ? $" TargetFramework='{TargetFramework}'" : "") +
               $" Certificate='{SignInfo.Certificate}'" +
               (SignInfo.StrongName != null ? $" StrongName='{SignInfo.StrongName}'" : "");
    }
}

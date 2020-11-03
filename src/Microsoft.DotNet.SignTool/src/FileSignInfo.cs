// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct FileSignInfo
    {
        internal readonly SignedFileContentKey FileContentKey;
        internal string FileName => File.FileName;
        internal string FullPath => File.FullPath;
        internal readonly SignInfo SignInfo;
        internal ImmutableArray<byte> ContentHash => File.ContentHash;
        internal readonly string WixContentFilePath;
        internal readonly PathWithHash File;

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

        internal static bool IsSymbolsNupkg(string path)
            => path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase);

        internal static bool IsZip(string path)
            => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);

        internal static bool IsWix(string path)
            => (Path.GetExtension(path).Equals(".msi", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(path).Equals(".wixlib", StringComparison.OrdinalIgnoreCase));

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

        internal bool IsNupkg() => IsNupkg(FileName) && !IsSymbolsNupkg();

        internal bool IsSymbolsNupkg() => IsSymbolsNupkg(FileName);

        internal bool IsZip() => IsZip(FileName);

        internal bool IsZipContainer() => IsZipContainer(FileName);

        internal bool IsWix() => IsWix(FileName);

        // A wix file is an Container if it has the proper extension AND the content
        // (ie *.wixpack.zip) is available, otherwise it's treated like a normal file
        internal bool IsWixContainer() =>
            WixContentFilePath != null
            && (IsWix(FileName) 
                || Path.GetExtension(FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase));

        internal bool IsExecutableWixContainer() =>
            IsWixContainer() &&
            (Path.GetExtension(FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(FileName).Equals(".msi", StringComparison.OrdinalIgnoreCase));

        internal bool IsContainer() => IsZipContainer() || IsWixContainer();

        internal bool IsPackage() => IsPackage(FileName);

        internal bool IsPowerShellScript() => IsPowerShellScript(FileName);

        internal bool HasSignableParts { get; }

        internal bool ShouldRepack => HasSignableParts;

        internal bool ShouldTrack => SignInfo.ShouldSign || ShouldRepack;

        internal FileSignInfo(PathWithHash pathWithHash, SignInfo signInfo, string targetFramework = null, string wixContentFilePath = null, bool hasSignableParts = false)
        {
            Debug.Assert(pathWithHash.FullPath != null);
            Debug.Assert(!pathWithHash.ContentHash.IsDefault && pathWithHash.ContentHash.Length == 256 / 8);
            Debug.Assert(targetFramework != "");

            File = pathWithHash;
            FileContentKey = new SignedFileContentKey(File.ContentHash, File.FileName);
            SignInfo = signInfo;
            TargetFramework = targetFramework;
            WixContentFilePath = wixContentFilePath;
            HasSignableParts = hasSignableParts;
        }

        public override string ToString()
            => $"File '{FileName}'" +
               (TargetFramework != null ? $" TargetFramework='{TargetFramework}'" : "") +
               $" Certificate='{SignInfo.Certificate}'" +
               (SignInfo.StrongName != null ? $" StrongName='{SignInfo.StrongName}'" : "");

        internal FileSignInfo WithSignableParts()
            => new FileSignInfo(File, SignInfo.WithIsAlreadySigned(false), TargetFramework, WixContentFilePath, true);

    }
}

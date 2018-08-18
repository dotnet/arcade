// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct FileName : IEquatable<FileName>
    {
        internal readonly string Name;
        internal readonly string FullPath;
        internal readonly string RelativePath;
        internal readonly SignInfo SignInfo;

        internal static bool IsPEFile(string fileFullPath)
        {
            return !String.IsNullOrWhiteSpace(fileFullPath) && (Path.GetExtension(fileFullPath) == ".exe" || Path.GetExtension(fileFullPath) == ".dll");
        }

        internal static bool IsVsix(string fileFullPath)
        {
            return !String.IsNullOrWhiteSpace(fileFullPath) && Path.GetExtension(fileFullPath).Equals(".vsix", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNupkg(string fileFullPath)
        {
            return !String.IsNullOrWhiteSpace(fileFullPath) && Path.GetExtension(fileFullPath).Equals(".nupkg", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsZipContainer(string fileFullPath)
        {
            return IsVsix(fileFullPath) || IsNupkg(fileFullPath);
        }

        internal bool IsPEFile() => IsPEFile(Name);

        internal bool IsVsix() => IsVsix(Name);

        internal bool IsNupkg() => IsNupkg(Name);

        internal bool IsZipContainer() => IsZipContainer(Name);

        internal FileName(string fullPath, SignInfo signInfo)
        {
            Name = Path.GetFileName(fullPath);
            FullPath = fullPath;
            RelativePath = Name;
            SignInfo = signInfo;
        }

        public static bool operator ==(FileName left, FileName right) => left.FullPath == right.FullPath;
        public static bool operator !=(FileName left, FileName right) => !(left == right);
        public bool Equals(FileName other) => this == other;
        public override int GetHashCode() => FullPath.GetHashCode();
        public override string ToString() => $"File: {FullPath}; SignInfo: {SignInfo}";
        public override bool Equals(object obj) => obj is FileName && Equals((FileName)obj);
    }
}

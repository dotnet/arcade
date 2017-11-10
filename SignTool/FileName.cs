// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignTool
{
    internal struct FileName : IEquatable<FileName>
    {
        internal string Name { get; }
        internal string FullPath { get; }
        internal string RelativePath { get; }
        internal string SHA256Hash { get; }

        internal bool IsAssembly => PathUtil.IsAssembly(Name);
        internal bool IsVsix => PathUtil.IsVsix(Name);
        internal bool IsNupkg => PathUtil.IsVsix(Name);
        internal bool IsZipContainer => PathUtil.IsZipContainer(Name);

        internal FileName(string rootBinaryPath, string relativePath, string sha256Hash = null)
        {
            Name = Path.GetFileName(relativePath);
            FullPath = Path.Combine(rootBinaryPath, relativePath);
            RelativePath = relativePath;
            SHA256Hash = sha256Hash;
        }

        public static bool operator ==(FileName left, FileName right) => left.FullPath == right.FullPath;
        public static bool operator !=(FileName left, FileName right) => !(left == right);
        public bool Equals(FileName other) => this == other;
        public override int GetHashCode() => FullPath.GetHashCode();
        public override string ToString() => RelativePath;
        public override bool Equals(object obj) => obj is FileName && Equals((FileName)obj);
    }
}

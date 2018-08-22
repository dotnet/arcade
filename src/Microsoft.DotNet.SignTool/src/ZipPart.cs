// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal struct ZipPart
    {
        internal string RelativeName { get; }
        internal FileSignInfo FileName { get; }
        internal string Checksum { get; }
        internal SignInfo SignInfo { get; }

        internal ZipPart(string relativeName, FileSignInfo fileName, string checksum, SignInfo signInfo)
        {
            RelativeName = relativeName;
            FileName = fileName;
            Checksum = checksum;
            SignInfo = signInfo;
        }

        public override string ToString() => $"{RelativeName} -> {FileName.Name} -> {Checksum}";
    }
}



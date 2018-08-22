// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct ZipPart
    {
        internal string RelativeName { get; }
        internal string Checksum { get; }
        internal FileSignInfo FileSignInfo { get; }

        internal ZipPart(string relativeName, FileSignInfo signInfo, string checksum)
        {
            RelativeName = relativeName;
            Checksum = checksum;
            FileSignInfo = signInfo;
        }

        public override string ToString() => $"{RelativeName} -> {FileSignInfo.Name} -> {Checksum}";
    }
}



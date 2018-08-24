// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Data for a zip container. Can refer to any zip format such as VSIX or nupkg
    /// </summary>
    internal sealed class ZipData
    {
        /// <summary>
        /// Name of the zip based package
        /// </summary>
        internal FileSignInfo FileSignInfo { get; }

        /// <summary>
        /// The parts inside this zip archive which need to be signed.
        /// </summary>
        internal ImmutableArray<ZipPart> NestedParts { get; }

        internal ZipData(FileSignInfo fileSignInfo, ImmutableArray<ZipPart> nestedBinaryParts)
        {
            FileSignInfo = fileSignInfo;
            NestedParts = nestedBinaryParts;
        }

        internal ZipPart? FindNestedPart(string relativeName)
        {
            foreach (var part in NestedParts)
            {
                if (relativeName == part.RelativeName)
                {
                    return part;
                }
            }

            return null;
        }
    }
}

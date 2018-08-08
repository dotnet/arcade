// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        internal FileName Name { get; }

        /// <summary>
        /// The parts inside this zip archive which need to be signed.
        /// </summary>
        internal ImmutableArray<ZipPart> NestedParts;

        /// <summary>
        /// Name of the external binaries this zip depends on.
        /// </summary>
        internal ImmutableArray<string> NestedExternalNames { get; }

        internal ZipData(FileName name, ImmutableArray<ZipPart> nestedBinaryParts, ImmutableArray<string> nestedExternalNames)
        {
            Name = name;
            NestedParts = nestedBinaryParts;
            NestedExternalNames = nestedExternalNames;
        }

        internal ZipPart? FindNestedBinaryPart(string relativeName)
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

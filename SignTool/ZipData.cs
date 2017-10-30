// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignTool
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

    internal struct ZipPart
    {
        internal string RelativeName { get; }
        internal FileName FileName { get; }
        internal string Checksum { get; }

        internal ZipPart(string relativeName, FileName fileName, string checksum)
        {
            RelativeName = relativeName;
            FileName = fileName;
            Checksum = checksum;
        }

        public override string ToString() => $"{RelativeName} -> {FileName.RelativePath} -> {Checksum}";
    }
}

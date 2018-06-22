// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignTool
{
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



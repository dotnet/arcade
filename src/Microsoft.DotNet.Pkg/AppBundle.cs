// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

#nullable enable

namespace Microsoft.DotNet.Pkg
{
    internal static class AppBundle
    {
        internal static void Unpack(string inputPath, string outputPath)
        {
            string args = $"-V -xk {inputPath} {outputPath}";
            ExecuteHelper.Run("ditto", args);
        }

        internal static void Pack(string inputPath, string outputPath)
        {
            string args = $"-c -k --sequesterRsrc {inputPath} {outputPath}";
            ExecuteHelper.Run("ditto", args);
        }
    }
}

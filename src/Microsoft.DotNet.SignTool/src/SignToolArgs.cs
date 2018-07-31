// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct SignToolArgs
    {
        internal string OutputDir { get; }
        internal string TempDir { get; }
        internal string MicroBuildCorePath { get; }
        internal bool TestSign { get; }

        internal SignToolArgs(
            string outputPath,
            string tempPath,
            string microBuildCorePath,
            bool testSign)
        {
            OutputDir = outputPath;
            TempDir = tempPath;
            MicroBuildCorePath = microBuildCorePath;
            TestSign = testSign;
        }
    }
}

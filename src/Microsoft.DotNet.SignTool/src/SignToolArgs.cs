// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool
{
    internal readonly struct SignToolArgs
    {
        internal string TempDir { get; }
        internal string MicroBuildCorePath { get; }
        internal bool TestSign { get; }
        internal string MSBuildPath { get; }
        internal string LogDir { get; }

        internal SignToolArgs(string tempPath, string microBuildCorePath, bool testSign, string msBuildPath, string logDir)
        {
            TempDir = tempPath;
            MicroBuildCorePath = microBuildCorePath;
            TestSign = testSign;
            MSBuildPath = msBuildPath;
            LogDir = logDir;
        }
    }
}

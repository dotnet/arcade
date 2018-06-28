// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitSync
{
    internal struct RunResult
    {
        public int ExitCode { get; }
        public string Output { get; }
        public RunResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }
    }
}
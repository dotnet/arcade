// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.Common.CLI;

public static class EnvironmentVariables
{
    public static class Names
    {
        public const string DISABLE_COLOR_OUTPUT = "XHARNESS_DISABLE_COLORED_OUTPUT";
        public const string LOG_TIMESTAMPS = "XHARNESS_LOG_WITH_TIMESTAMPS";
        public const string MLAUNCH_PATH = "XHARNESS_MLAUNCH_PATH";
        public const string DIAGNOSTICS_PATH = "XHARNESS_DIAGNOSTICS_PATH";
    }

    public static bool IsTrue(string varName) =>
        Environment.GetEnvironmentVariable(varName)?.ToLower().Equals("true") ?? false;
}

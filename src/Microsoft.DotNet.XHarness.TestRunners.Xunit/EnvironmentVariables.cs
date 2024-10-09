using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal static class EnvironmentVariables
{
    public static bool IsTrue(string varName) => Environment.GetEnvironmentVariable(varName)?.ToLower().Equals("true") ?? false;

    public static bool IsLogTestStart() => IsTrue("XHARNESS_LOG_TEST_START");

    public static bool IsLogThreadId() => IsTrue("XHARNESS_LOG_THREAD_ID");
}

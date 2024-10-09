// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests.Utilities;

public class StringUtilsTests
{
    private static readonly char s_shellQuoteChar =
        (int)Environment.OSVersion.Platform != 128
            && Environment.OSVersion.Platform != PlatformID.Unix
            && Environment.OSVersion.Platform != PlatformID.MacOSX
        ? '"'   // Windows
        : '\''; // !Windows

    [Fact]
    public void NoEscapingNeeded() => Assert.Equal("foo", StringUtils.Quote("foo"));

    [Theory]
    [InlineData("foo bar", "foo bar")]
    [InlineData("foo \"bar\"", "foo \\\"bar\\\"")]
    [InlineData("foo bar's", "foo bar\\\'s")]
    [InlineData("foo $bar's", "foo $bar\\\'s")]
    public void QuoteForProcessTest(string input, string expected) => Assert.Equal(s_shellQuoteChar + expected + s_shellQuoteChar, StringUtils.Quote(input));

    [Fact(Skip = "Only works on OSX/Linux")]
    public void FormatArgumentsTest()
    {
        var p = new Process();
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "/bin/echo";

        var complexInput = "'";

        p.StartInfo.Arguments = StringUtils.FormatArguments("-n", "foo", complexInput, "bar");
        p.Start();
        var output = p.StandardOutput.ReadToEnd();
        Assert.Equal($"foo {complexInput} bar", output);
    }
}

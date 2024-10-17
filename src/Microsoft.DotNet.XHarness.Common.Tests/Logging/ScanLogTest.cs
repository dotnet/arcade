// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests.Logging;

public class ScanLogTest
{
    [Theory]
    [InlineData("This is a log message", "log", true)]
    [InlineData("emessag", "message", false)]
    [InlineData("This is a log message", "This is a log message", true)]
    [InlineData("This is a log message.", ".", true)]
    public void TagIsFoundInLog(string message, string tag, bool shouldFind)
    {
        bool found = false;
        var log = new ScanLog(tag, () => found = true);
        log.Write(message);
        Assert.Equal(shouldFind, found);
    }

    [Fact]
    public void TagIsFoundInSeveralMessages()
    {
        bool found = false;
        var log = new ScanLog("123", () => found = true);
        log.Write("abc1");
        log.Write("2");
        log.Write("3cdef");
        Assert.True(found);
    }
}

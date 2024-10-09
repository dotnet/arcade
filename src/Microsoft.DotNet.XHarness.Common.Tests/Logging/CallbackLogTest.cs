// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests.Logging;

public class CallbackLogTest
{
    [Fact]
    public void OnWriteTest()
    {
        var message = "This is a log message";
        bool called = false;
        string data = null;

        Action<string> cb = (d) =>
        {
            called = true;
            data = d;
        };

        var log = new CallbackLog(cb);
        log.Write(message);
        Assert.True(called, "Callback was not called");
        Assert.NotNull(data);
        Assert.EndsWith(message, data); // TODO: take time stamp into account
    }
}

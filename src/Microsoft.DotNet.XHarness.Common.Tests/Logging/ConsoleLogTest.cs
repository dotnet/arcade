// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests.Logging;

[CollectionDefinition("ConsoleLogTest", DisableParallelization = true)]
public class ConsoleLogTest : IDisposable
{
    private readonly string _testFile;
    private readonly TextWriter _sdoutWriter;
    private readonly ConsoleLog _log;

    public ConsoleLogTest()
    {
        _testFile = Path.GetTempFileName();
        _log = new ConsoleLog();
        _sdoutWriter = Console.Out;
    }

    [Fact(Skip = "Flakey test that gets in the way by messing around with Console.Out")]
    public void TestWrite()
    {
        var message = "This is a log message";
        using (var testStream = new FileStream(_testFile, FileMode.OpenOrCreate, FileAccess.Write))
        using (var writer = new StreamWriter(testStream))
        {
            Console.SetOut(writer);
            // simply test that we do write in the file. We need to close the stream to be able to read it
            _log.WriteLine(message);
        }

        using (var testStream = new FileStream(_testFile, FileMode.OpenOrCreate, FileAccess.Read))
        using (var reader = new StreamReader(testStream))
        {
            var line = reader.ReadLine();
            Assert.EndsWith(message, line); // consider the time stamp
        }

    }

    public void Dispose()
    {
        _log.Dispose();
        Console.SetOut(_sdoutWriter); // get back to write to the console
        File.Delete(_testFile);
        GC.SuppressFinalize(this);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Arcade.Test.Common;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Installers.Tests
{
    public class ExecWithRetriesTests
    {
        [Fact]
        public void Test1()
        {
            var task = new ExecWithRetries();
            task.Command = "light.exe";
            task.BuildEngine = new MockBuildEngine();

            task.Execute().Should().BeTrue();
        }
    }
}

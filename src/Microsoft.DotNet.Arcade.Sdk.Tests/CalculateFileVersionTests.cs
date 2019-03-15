// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class CalculateFileVersionTests
    {
        [Theory]
        [InlineData("1.0.0", "20190102.3", "1.0.19.5203")]
        [InlineData("2.1.800", "20190314.5", "2.108.19.16405")]
        [InlineData("65535.654.9999", "20991231.99", "65535.65499.9999.63199")]
        public void Execute(string prefix, string buildNumber, string expectedFileVersion)
        {
            var task = new CalculateFileVersion()
            {
                VersionPrefix = prefix,
                OfficialBuildId = buildNumber
            };

            bool result = task.Execute();
            Assert.Equal(expectedFileVersion, task.FileVersion);
            Assert.True(result);
        }

    }
}

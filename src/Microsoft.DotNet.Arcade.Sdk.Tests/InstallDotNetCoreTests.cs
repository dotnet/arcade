// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class InstallDotNetCoreTests
    {
        [Theory]
        [InlineData("8.0", true)]
        [InlineData("10.0", true)]
        [InlineData("3.1", true)]
        [InlineData("8.0.22", false)]
        [InlineData("10.0.1", false)]
        [InlineData("8.0.0-preview.1", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("8", false)]
        [InlineData("8.0.1.2", false)]
        [InlineData("v8.0", false)]
        [InlineData("8.x", false)]
        public void IsTwoPartVersion_DetectsCorrectFormat(string versionString, bool expected)
        {
            // Use reflection to call the private method
            var task = new InstallDotNetCore();
            var method = typeof(InstallDotNetCore).GetMethod("IsTwoPartVersion", BindingFlags.NonPublic | BindingFlags.Instance);
            
            var result = (bool)method.Invoke(task, new object[] { versionString });
            
            Assert.Equal(expected, result);
        }
    }
}

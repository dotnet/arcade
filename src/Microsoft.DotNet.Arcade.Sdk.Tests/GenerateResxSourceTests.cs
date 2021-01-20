// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class GenerateResxSourceTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateResxSourceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("A", "A")]
        [InlineData("_A", "_A")]
        [InlineData(".A", "_A")]
        [InlineData("4A", "_4A")]
        [InlineData("4(.-)A", "_4____A")]
        [InlineData("A\u0660\u2040\u0601\u0300\u0903", "A\u0660\u2040\u0601\u0300\u0903")]
        public void GetIdentifierFromResourceName(string name, string expectedIdentifier)
        {
            Assert.Equal(expectedIdentifier, GenerateResxSource.GetIdentifierFromResourceName(name));
        }
    }
}

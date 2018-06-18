// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.TestsUtil;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Versioning.Tests
{
    public class GenerateVersionTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateVersionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicTestDefaults()
        {
            DateTime BaselineDate = new DateTime(1996, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime BuildDate = DateTime.UtcNow;

            int months = (BuildDate.Year - BaselineDate.Year) * 12 + BuildDate.Month - BaselineDate.Month;

            string expectedDate = string.Format("{0}{1}", months.ToString("D"), BuildDate.Day.ToString("D2"));

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine()
            };

            Assert.True(task.Execute());
            Assert.Equal("0", task.GeneratedRevision);
            Assert.Equal(expectedDate, task.GeneratedShortDate);
            Assert.Equal("", task.GeneratedShortSha);
        }
    }
}


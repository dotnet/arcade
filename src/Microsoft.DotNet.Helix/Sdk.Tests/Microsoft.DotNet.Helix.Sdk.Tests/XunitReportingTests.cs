// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class XunitReportingTests
    {
        // XUnit Reporter scripts try to reverse these changes:
        // https://github.com/xunit/xunit/blob/main/src/xunit.v3.runner.common/Sinks/DelegatingSinks/DelegatingXmlCreationSink.cs#L346
        // We need these values to pass through the reporting code paths.
        [Fact]
        public void ExerciseXunitCharacterFilteringFailurePath()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HELIX_WORKITEM_ID")))
            {
                // Not on a Helix machine? Do an assert and exit
                (2+2).Should().Be(4);
                return;
            }
            // On a Helix machine:  Fail with all the special characters handled in 
            //https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Helix/Sdk/tools/azure-pipelines/reporter/formats/xunit.py#L7-L28
            Assert.True(false, "Intentional Failure 🍕 \r \n \t \a \0 \b \v \f \0");
        }
    }
}

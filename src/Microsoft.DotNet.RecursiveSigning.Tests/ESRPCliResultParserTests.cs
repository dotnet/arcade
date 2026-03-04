// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using FluentAssertions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class ESRPCliResultParserTests
    {
        [Fact]
        public void Parse_SuccessOutput_ReturnsSuccess()
        {
            var result = ESRPCliResultParser.Parse(0, "Success: All files signed.\n", "");

            result.Success.Should().BeTrue();
            result.ErrorMessage.Should().BeNull();
        }

        [Fact]
        public void Parse_FailDoNotRetry_ReturnsFailure()
        {
            var result = ESRPCliResultParser.Parse(0, "Some output\nfailDoNotRetry: permanent error\n", "");

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("failDoNotRetry");
        }

        [Fact]
        public void Parse_NonZeroExitCode_ReturnsFailure()
        {
            var result = ESRPCliResultParser.Parse(1, "Success: All files signed.\n", "some error");

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("exited with code 1");
        }

        [Fact]
        public void Parse_EmptyOutputWithZeroExitCode_ReturnsFailure()
        {
            var result = ESRPCliResultParser.Parse(0, "", "");

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("did not report success");
        }

        [Fact]
        public void Parse_ExtractsOperationId()
        {
            var guid = Guid.NewGuid();
            var stdout = $"Calling esrp gateway get status for this operation Id: {guid} more text\nSuccess\n";

            var result = ESRPCliResultParser.Parse(0, stdout, "");

            result.Success.Should().BeTrue();
            result.OperationId.Should().Be(guid);
        }

        [Fact]
        public void Parse_OperationIdWithNoSuccess_StillExtractsId()
        {
            var guid = Guid.NewGuid();
            var stdout = $"Calling esrp gateway get status for this operation Id: {guid}\nfailDoNotRetry\n";

            var result = ESRPCliResultParser.Parse(0, stdout, "");

            result.Success.Should().BeFalse();
            result.OperationId.Should().Be(guid);
        }

        [Fact]
        public void Parse_SuccessCaseInsensitive()
        {
            var result = ESRPCliResultParser.Parse(0, "SUCCESS: done\n", "");

            result.Success.Should().BeTrue();
        }

        [Fact]
        public void Parse_FailDoNotRetryCaseInsensitive()
        {
            var result = ESRPCliResultParser.Parse(0, "FAILDONOTRETRY\n", "");

            result.Success.Should().BeFalse();
        }

        [Fact]
        public void Parse_NonZeroExitCode_IncludesStderrInMessage()
        {
            var result = ESRPCliResultParser.Parse(2, "", "detailed error info");

            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("detailed error info");
        }
    }
}

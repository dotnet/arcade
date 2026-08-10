// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using AwesomeAssertions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class HelixJobMonitorTemplateTests
    {
        [Fact]
        public void ProcessingParallelismIsOnlyForwardedWhenExplicitlySet()
        {
            string template = File.ReadAllText(FindTemplatePath()).Replace("\r\n", "\n");

            int parameterStart = template.IndexOf(
                "- name: testResultProcessingParallelism",
                StringComparison.Ordinal);
            parameterStart.Should().BeGreaterThanOrEqualTo(0);
            string parameterBlock = template.Substring(parameterStart, 100);
            parameterBlock.Should().Contain("type: string");
            parameterBlock.Should().Contain("default: ''");
            template.Should().Contain(
                "testResultProcessingParallelism='${{ parameters.testResultProcessingParallelism }}'");
            template.Should().Contain("if [ -n \"$testResultProcessingParallelism\" ]; then");
            template.Should().Contain(
                "toolArgs+=(--test-result-processing-parallelism \"$testResultProcessingParallelism\")");
            template.Should().NotContain(
                "--test-result-processing-parallelism '${{ parameters.testResultProcessingParallelism }}'");
        }

        private static string FindTemplatePath()
        {
            for (DirectoryInfo directory = new(AppContext.BaseDirectory);
                directory is not null;
                directory = directory.Parent)
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    "eng",
                    "common",
                    "core-templates",
                    "job",
                    "helix-job-monitor.yml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("Could not locate eng/common/core-templates/job/helix-job-monitor.yml.");
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class ProgramArgsTests
{
    [Fact]
    public async Task ParseAndRunAsync_AllArguments_AreParsedCorrectly()
    {
        // Arrange
        ProgramArgs? receivedArgs = null;

        int Run(ProgramArgs args)
        {
            receivedArgs = args;
            return 0;
        }

        // Act
        await ProgramArgs.ParseAndRunAsync(Run, new[]
        {
            "--branch-binlog", "test.bbl",
            "--binlog", "main.bl",
            "--repository", "repo",
            "--branch", "branchName",
            "--output-json", "output.json",
            "--debug"
        });

        // Assert
        Assert.Equal("test.bbl", receivedArgs?.BranchBinLog?.Name);
        Assert.Equal("main.bl", receivedArgs?.BinLog?.Name);
        Assert.Equal("repo", receivedArgs?.Repository?.Name);
        Assert.Equal("branchName", receivedArgs?.Branch);
        Assert.Equal("output.json", receivedArgs?.OutputJson?.Name);
        Assert.True(receivedArgs?.Debug);
    }
}

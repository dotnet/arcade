// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests;

public class GlobalJsonTests
{
    [Theory]
    [InlineData("""{ "tools": { "dotnet": "1.0.100" } }""", "1.0.100")]
    [InlineData("""{ "sdk": { "version": "2.0.100" }, "tools": { "downloadSdkVersion": false, "dotnet": "1.0.100" } }""", "1.0.100")]
    [InlineData("""{ "sdk": { "version": "2.0.100" }, "tools": { "downloadSdkVersion": true } }""", "2.0.100")]
    [InlineData("""
        {
          // comments and trailing commas are allowed in global.json
          "tools": { "dotnet": "1.0.100", "downloadSdkVersion": true, },
          "sdk": { "errorMessage": "Install SDK {2.0.100}.", "version": "2.0.100-preview.1" },
        }
        """, "2.0.100-preview.1")]
    public void TryGetDotNetSdkVersionReturnsVersion(string content, string expectedVersion)
    {
        Assert.True(GlobalJson.TryGetDotNetSdkVersion(content, "global.json", out string version, out string error));
        Assert.Equal(expectedVersion, version);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("""{ "sdk": { "version": "2.0.100" } }""", "Unable to determine dotnet version")]
    [InlineData("""{ "tools": { "downloadSdkVersion": true, "dotnet": "1.0.100" } }""", "must specify 'sdk.version'")]
    [InlineData("""{ "tools": """, "Unable to parse file")]
    public void TryGetDotNetSdkVersionReportsError(string content, string expectedError)
    {
        Assert.False(GlobalJson.TryGetDotNetSdkVersion(content, "global.json", out string version, out string error));
        Assert.Null(version);
        Assert.Contains(expectedError, error);
    }
}

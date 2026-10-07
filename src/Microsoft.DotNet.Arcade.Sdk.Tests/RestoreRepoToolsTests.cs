// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests;

public class RestoreRepoToolsTests
{
    [Theory]
    [InlineData("true", "", "2")]
    [InlineData("false", "", "2")]
    [InlineData("", "", "2")]
    [InlineData("true", "1", "1")]
    [InlineData("false", "1", "1")]
    [InlineData("false", "5", "5")]
    public void RestoreAttemptsRespectDefaultsAndOverrides(string continuousIntegrationBuild, string maxAttempts, string expected)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "testassets", "ArcadeSdkTools", "Tools.proj"));
        document.Root.Attribute("Sdk").Remove();
        document.Root.Elements("Import").Remove();

        using var collection = new ProjectCollection(new Dictionary<string, string>
        {
            ["RepoRoot"] = AppContext.BaseDirectory,
            ["ContinuousIntegrationBuild"] = continuousIntegrationBuild,
        });
        using var reader = document.CreateReader();
        var project = new Project(reader, null, null, collection);
        if (maxAttempts != "")
        {
            project.SetGlobalProperty("RestoreRepoToolsMaxAttempts", maxAttempts);
            project.ReevaluateIfNecessary();
        }

        Assert.Equal(expected, project.GetPropertyValue("RestoreRepoToolsMaxAttempts"));
        var target = document.Root.Element("Target");
        Assert.Equal("RestoreRepoTools", target.Attribute("Name").Value);
        Assert.Equal("$(RestoreRepoToolsMaxAttempts)", target.Element("Microsoft.DotNet.Arcade.Sdk.ExecWithRetries").Attribute("MaxAttempts").Value);
    }
}

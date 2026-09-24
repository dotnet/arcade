// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class ResultAggregatorTests
{
    private static TestResult Test(string typeName, string method, string displayName)
        => new(displayName, "trx", typeName, method, 0.1, "Pass", null, null, null, null, null);

    [Fact]
    public void FullyQualifiedName_IsDerivedFromTypeAndMethod()
    {
        TestResult test = Test("Ns.MyTests", "MyMethod", "MyMethod");
        Assert.Equal("Ns.MyTests.MyMethod", test.FullyQualifiedName);
    }

    [Fact]
    public void Aggregate_SeparatesSameMethodNameAcrossClasses()
    {
        // MSTest reports the display name as just the method name, so both tests look like "MyMethod".
        TestResult a = Test("Ns.ClassA", "MyMethod", "MyMethod");
        TestResult b = Test("Ns.ClassB", "MyMethod", "MyMethod");

        IReadOnlyList<AggregatedResult> aggregate =
            new ResultAggregator().Aggregate([[a, b]]);

        Assert.Equal(2, aggregate.Count);
        Assert.Contains(aggregate, r => r.FullyQualifiedName == "Ns.ClassA.MyMethod");
        Assert.Contains(aggregate, r => r.FullyQualifiedName == "Ns.ClassB.MyMethod");
    }

    [Fact]
    public void Aggregate_ParameterizedRows_ShareFullyQualifiedNameAndKeepArgumentDisplay()
    {
        TestResult net10 = Test("Ns.NativeAotTests", "WillRunWithExitCodeZero", "WillRunWithExitCodeZero (\"net10.0\")");
        TestResult net8 = Test("Ns.NativeAotTests", "WillRunWithExitCodeZero", "WillRunWithExitCodeZero (\"net8.0\")");

        AggregatedResult result = Assert.Single(
            new ResultAggregator().Aggregate([[net10, net8]]));

        Assert.Equal("Ns.NativeAotTests.WillRunWithExitCodeZero", result.FullyQualifiedName);
        Assert.Equal(AggregationType.DataDriven, result.AggregationType);
        Assert.Equal(2, result.SubResults.Count);
        Assert.All(result.SubResults, sub => Assert.Equal("Ns.NativeAotTests.WillRunWithExitCodeZero", sub.FullyQualifiedName));
        Assert.Contains(result.SubResults, sub => sub.Name.Contains("net10.0"));
        Assert.Contains(result.SubResults, sub => sub.Name.Contains("net8.0"));
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

[Collection("NonParallel")]
public class ReportingTargetsTests
{
    private static readonly string s_tools = Path.Combine(AppContext.BaseDirectory, "SdkTools");

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, false, false)]
    public void ReportingFlags_SelectExactlyOnePublisher(
        bool monitor, bool reporter, bool tokenAvailable, bool expectInline)
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.AddProperty("SYSTEM_ACCESSTOKEN", tokenAvailable ? "test-token" : "");
        root.AddProperty("EnableHelixJobMonitor", monitor.ToString());
        root.AddImport(Path.Combine(s_tools, "azure-pipelines", "AzurePipelines.props"));
        root.AddImport(Path.Combine(s_tools, "Microsoft.DotNet.Helix.Sdk.MonoQueue.targets"));
        root.AddImport(Path.Combine(s_tools, "Microsoft.DotNet.Helix.Sdk.MultiQueue.targets"));
        var project = new Project(root, new Dictionary<string, string>
        {
            ["EnableAzurePipelinesReporter"] = reporter.ToString(),
            ["HelixTargetQueue"] = "Windows.10.Amd64.Open",
        }, toolsVersion: null, collection);

        Assert.Equal(expectInline, bool.Parse(project.GetPropertyValue("ImportAzurePipelinesTargets")));
        if (monitor)
        {
            Assert.Equal("false", project.GetPropertyValue("WaitForWorkItemCompletion"));
        }
        string postCommands = project.GetPropertyValue("HelixPostCommands");
        Assert.Equal(monitor || expectInline, postCommands.Contains("collect-test-results.ps1", StringComparison.Ordinal));
        Assert.DoesNotContain("run.py", postCommands);
        Assert.DoesNotContain("test-token", postCommands);
        Assert.DoesNotContain(project.GetItems("HelixCorrelationPayload"), item => item.EvaluatedInclude.EndsWith("reporter", StringComparison.Ordinal));
    }

    [Fact]
    public void InlineTargets_PublishIntoCorrectRunsBeforeClosingAndChecking()
    {
        using var collection = new ProjectCollection();
        var root = ProjectRootElement.Create(collection);
        root.AddProperty("TestRunNamePrefix", "prefix-");
        root.AddProperty("TestRunNameSuffix", "-suffix");
        root.AddProperty("FailOnTestFailure", "true");
        root.AddItem("HelixTargetQueue", "windows").AddMetadata("TestRunName", "custom-windows");
        root.AddItem("HelixTargetQueue", "linux");
        root.AddImport(Path.Combine(s_tools, "azure-pipelines", "AzurePipelines.MultiQueue.targets"));
        foreach (Type type in new[]
        {
            typeof(Microsoft.Build.Tasks.Message),
            typeof(Fakes.InlineReporting.StartAzurePipelinesTestRun),
            typeof(Fakes.InlineReporting.UploadHelixTestResults),
            typeof(Fakes.InlineReporting.CreateTestsForWorkItems),
            typeof(Fakes.InlineReporting.StopAzurePipelinesTestRun),
            typeof(Fakes.InlineReporting.CheckAzurePipelinesTestResults),
        })
        {
            root.AddUsingTask(type.FullName, type.Assembly.Location, assemblyName: null);
        }
        root.AddTarget("CoreBuild");
        var coreTest = root.AddTarget("CoreTest");
        coreTest.AddTask("Message").SetParameter("Text", "event:wait");
        coreTest.AddTask("Message").SetParameter("Text", "event:discover");
        var completed = coreTest.AddItemGroup().AddItem("CompletedWorkItem", "@(HelixTargetQueue)");
        completed.AddMetadata("JobName", "job-%(Identity)");
        completed.AddMetadata("WorkItemName", "work-item");
        root.AddTarget("AfterTest").AddTask("Message").SetParameter("Text", "event:check-work-items");
        root.AddTarget("Test").DependsOnTargets = "CoreBuild;CoreTest;AfterTest";
        var project = new Project(root, null, null, collection);
        var logger = new RecordingLogger();

        Assert.True(project.Build("Test", [logger]), string.Join(Environment.NewLine, logger.Errors));
        Assert.Equal(new[]
        {
            "event:start:custom-windows:14",
            "event:start:prefix-linux-suffix:19",
            "event:wait",
            "event:discover",
            "event:publish:windows:14",
            "event:publish:linux:19",
            "event:execution-results",
            "event:stop:custom-windows:14",
            "event:stop:prefix-linux-suffix:19",
            "event:check-work-items",
            "event:check-results",
        }.Order(StringComparer.Ordinal), logger.Events.Order(StringComparer.Ordinal));
        Assert.Equal(new[]
        {
            "start", "start", "wait", "discover", "publish", "publish",
            "execution-results", "stop", "stop", "check-work-items", "check-results",
        }, logger.Events.Select(e => e.Split(':')[1]));
    }

    private sealed class RecordingLogger : ILogger
    {
        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Diagnostic;
        public string Parameters { get; set; }
        public List<string> Events { get; } = [];
        public List<string> Errors { get; } = [];

        public void Initialize(IEventSource eventSource)
        {
            eventSource.MessageRaised += (_, e) =>
            {
                if (e.Message?.StartsWith("event:", StringComparison.Ordinal) == true)
                {
                    Events.Add(e.Message);
                }
            };
            eventSource.ErrorRaised += (_, e) => Errors.Add(e.Message);
        }

        public void Shutdown() { }
    }
}

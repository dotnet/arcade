// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Helix.Sdk.Tests.Fakes.InlineReporting;

public sealed class StartAzurePipelinesTestRun : Task
{
    public string TestRunName { get; set; }

    [Output]
    public int TestRunId { get; set; }

    public override bool Execute()
    {
        TestRunId = TestRunName.Length;
        Log.LogMessage(MessageImportance.High, $"event:start:{TestRunName}:{TestRunId}");
        return true;
    }
}

public sealed class UploadHelixTestResults : Task
{
    public string AccessToken { get; set; }
    public bool UseEntraAuthentication { get; set; }
    public string BaseUri { get; set; }
    public string OutputDirectory { get; set; }
    public ITaskItem[] WorkItems { get; set; }

    public override bool Execute()
    {
        foreach (ITaskItem item in WorkItems)
        {
            Log.LogMessage(MessageImportance.High, $"event:publish:{item.ItemSpec}:{item.GetMetadata("TestRunId")}");
        }
        return true;
    }
}

public sealed class CreateTestsForWorkItems : Task
{
    public ITaskItem[] WorkItems { get; set; }

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.High, "event:execution-results");
        return true;
    }
}

public sealed class StopAzurePipelinesTestRun : Task
{
    public int TestRunId { get; set; }
    public string TestRunName { get; set; }

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.High, $"event:stop:{TestRunName}:{TestRunId}");
        return true;
    }
}

public sealed class CheckAzurePipelinesTestResults : Task
{
    public ITaskItem[] ExpectedTestFailures { get; set; }
    public string EnableFlakyTestSupport { get; set; }
    public int[] TestRunIds { get; set; }
    public ITaskItem[] WorkItems { get; set; }

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.High, "event:check-results");
        return true;
    }
}

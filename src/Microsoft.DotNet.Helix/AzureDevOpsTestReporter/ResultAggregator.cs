// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public enum AggregationType
{
    Single = 0,
    Rerun = 1,
    DataDriven = 2,
}

public sealed class AggregatedResult
{
    public AggregatedResult(
        AggregationType aggregationType,
        string name,
        double durationSeconds,
        string result,
        IReadOnlyList<AggregatedResult>? subResults = null,
        IReadOnlyList<TestResultAttachment>? attachments = null,
        string? failureMessage = null,
        string? stackTrace = null,
        string? skipReason = null,
        bool isFlaky = false,
        int? attemptId = null)
    {
        AggregationType = aggregationType;
        Name = name ?? string.Empty;
        DurationSeconds = durationSeconds;
        Result = result ?? string.Empty;
        SubResults = subResults ?? Array.Empty<AggregatedResult>();
        Attachments = attachments ?? Array.Empty<TestResultAttachment>();
        FailureMessage = failureMessage ?? skipReason;
        StackTrace = stackTrace;
        IsFlaky = isFlaky;
        AttemptId = attemptId;
    }

    public AggregationType AggregationType { get; }

    public string Name { get; }

    public double DurationSeconds { get; }

    public string Result { get; }

    public IReadOnlyList<TestResultAttachment> Attachments { get; }

    public IReadOnlyList<AggregatedResult> SubResults { get; }

    public string? FailureMessage { get; }

    public string? StackTrace { get; }

    public int? AttemptId { get; }

    public bool IsFlaky { get; }
}

public sealed class ResultAggregator
{
    public IReadOnlyList<AggregatedResult> Aggregate(IEnumerable<IEnumerable<TestResult>>? results)
    {
        if (results is null)
        {
            return Array.Empty<AggregatedResult>();
        }

        string GetResult(TestResult test)
        {
            if (test.Ignored && string.Equals(test.Result, "Fail", StringComparison.Ordinal))
            {
                return "NotApplicable";
            }

            return test.Result switch
            {
                "Pass" => "Passed",
                "Fail" => "Failed",
                "Skip" => "NotExecuted",
                _ => "None",
            };
        }

        static string ParseBasicName(string name)
        {
            var separatorIndex = name.IndexOf('(');
            return separatorIndex >= 0 ? name[..separatorIndex] : name;
        }

        AggregatedResult CreateResultFromTest(TestResult result, int? attemptId = null)
        {
            return new AggregatedResult(
                AggregationType.Single,
                attemptId is null ? result.Name : $"Attempt #{attemptId} - {result.Name}",
                result.DurationSeconds,
                GetResult(result),
                Array.Empty<AggregatedResult>(),
                result.Attachments,
                result.FailureMessage,
                result.StackTrace,
                result.SkipReason,
                attemptId: attemptId);
        }

        string GetDataDrivenResult(IReadOnlyList<TestResult> groupedResults)
        {
            if (groupedResults.Count == 0)
            {
                return "None";
            }

            if (groupedResults.Any(static r => !r.Ignored && r.Result == "Fail"))
            {
                return "Failed";
            }

            if (groupedResults.Any(static r => r.Result == "Pass"))
            {
                return "Passed";
            }

            return GetResult(groupedResults[0]);
        }

        (bool IsFlaky, string Outcome) GetRerunResult(IReadOnlyList<TestResult> groupedResults)
        {
            if (groupedResults.Count == 0)
            {
                return (false, "None");
            }

            var anyPass = groupedResults.Any(static r => r.Result == "Pass");
            var anyFail = groupedResults.Any(static r => !r.Ignored && r.Result == "Fail");
            var isFlaky = anyPass && anyFail;

            if (anyPass)
            {
                return (isFlaky, "Passed");
            }

            if (anyFail)
            {
                return (isFlaky, "Failed");
            }

            return (false, GetResult(groupedResults[0]));
        }

        AggregatedResult ProcessNamedTest(string name, IReadOnlyList<List<TestResult>> byIterationThenName)
        {
            if (byIterationThenName.Count == 1)
            {
                var singleRun = byIterationThenName[0];
                if (singleRun.Count == 1)
                {
                    return CreateResultFromTest(singleRun[0]);
                }

                return new AggregatedResult(
                    AggregationType.DataDriven,
                    name,
                    singleRun.Sum(static r => r.DurationSeconds),
                    GetDataDrivenResult(singleRun),
                    singleRun.Select(testResult => CreateResultFromTest(testResult)).ToList());
            }

            var hasDataDriven = byIterationThenName.Any(static x => x.Count > 1);

            if (hasDataDriven)
            {
                var dataDrivenByFullName = new Dictionary<string, List<TestResult>>(StringComparer.Ordinal);
                foreach (var iteration in byIterationThenName)
                {
                    foreach (var test in iteration)
                    {
                        if (!dataDrivenByFullName.TryGetValue(test.Name, out var list))
                        {
                            list = new List<TestResult>();
                            dataDrivenByFullName[test.Name] = list;
                        }

                        list.Add(test);
                    }
                }

                var subResults = new List<AggregatedResult>();
                double totalDuration = 0;

                foreach (var pair in dataDrivenByFullName)
                {
                    var dataDrivenTests = pair.Value;
                    if (dataDrivenTests.Count == 1)
                    {
                        subResults.Add(CreateResultFromTest(dataDrivenTests[0]));
                        totalDuration += dataDrivenTests[0].DurationSeconds;
                        continue;
                    }

                    var (isFlaky, aggregateResult) = GetRerunResult(dataDrivenTests);
                    var partialDuration = dataDrivenTests.Sum(static r => r.DurationSeconds);
                    totalDuration += partialDuration;
                    subResults.Add(new AggregatedResult(
                        AggregationType.Rerun,
                        pair.Key,
                        partialDuration,
                        aggregateResult,
                        dataDrivenTests.Select((r, index) => CreateResultFromTest(r, index + 1)).ToList(),
                        isFlaky: isFlaky));
                }

                var aggregateOutcome = "Inconclusive";
                if (dataDrivenByFullName.Values.Any(rerunSet => rerunSet.Where(static r => !r.Ignored).All(static r => r.Result == "Fail")))
                {
                    aggregateOutcome = "Failed";
                }
                else if (dataDrivenByFullName.Values.All(rerunSet => rerunSet.All(static r => r.Result == "Skip")))
                {
                    aggregateOutcome = "NotExecuted";
                }
                else if (dataDrivenByFullName.Values.All(rerunSet => rerunSet.Any(static r => r.Result == "Pass")))
                {
                    aggregateOutcome = "Passed";
                }

                return new AggregatedResult(
                    AggregationType.DataDriven,
                    name,
                    totalDuration,
                    aggregateOutcome,
                    subResults);
            }

            var reruns = byIterationThenName.Select(static run => run[0]).ToList();
            var (rerunIsFlaky, rerunOutcome) = GetRerunResult(reruns);
            return new AggregatedResult(
                AggregationType.Rerun,
                name,
                reruns.Sum(static r => r.DurationSeconds),
                rerunOutcome,
                reruns.Select((r, index) => CreateResultFromTest(r, index + 1)).ToList(),
                failureMessage: reruns[0].FailureMessage,
                stackTrace: reruns[0].StackTrace,
                isFlaky: rerunIsFlaky);
        }

        AggregatedResult ReduceSimpleResult(AggregatedResult result)
        {
            if (result.SubResults.Count == 0)
            {
                return result;
            }

            if (result.AggregationType == AggregationType.Rerun)
            {
                var distinctOutcomes = result.SubResults
                    .Select(static r => r.Result)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                if (distinctOutcomes == 1)
                {
                    var single = result.SubResults[0];
                    return new AggregatedResult(
                        AggregationType.Single,
                        result.Name,
                        single.DurationSeconds,
                        single.Result,
                        attachments: single.Attachments,
                        failureMessage: single.FailureMessage,
                        stackTrace: single.StackTrace);
                }

                return result;
            }

            return new AggregatedResult(
                result.AggregationType,
                result.Name,
                result.DurationSeconds,
                result.Result,
                result.SubResults.Select(ReduceSimpleResult).ToList(),
                result.Attachments,
                result.FailureMessage,
                result.StackTrace,
                isFlaky: result.IsFlaky,
                attemptId: result.AttemptId);
        }

        var partials = new List<Dictionary<string, List<TestResult>>>();
        foreach (var resultSet in results)
        {
            var perAttempt = new Dictionary<string, List<TestResult>>(StringComparer.Ordinal);
            foreach (var result in resultSet)
            {
                var basicName = ParseBasicName(result.Name);
                if (!perAttempt.TryGetValue(basicName, out var list))
                {
                    list = new List<TestResult>();
                    perAttempt[basicName] = list;
                }

                list.Add(result);
            }

            partials.Add(perAttempt);
        }

        if (partials.Count == 0 || partials[0].Count == 0)
        {
            return Array.Empty<AggregatedResult>();
        }

        var aggregate = new List<AggregatedResult>();
        foreach (var run in partials)
        {
            foreach (var pair in run.ToList())
            {
                if (!run.Remove(pair.Key, out var currentSet))
                {
                    continue;
                }

                var fullSet = new List<List<TestResult>> { currentSet };
                foreach (var otherRun in partials)
                {
                    if (ReferenceEquals(otherRun, run))
                    {
                        continue;
                    }

                    if (otherRun.Remove(pair.Key, out var otherSet))
                    {
                        fullSet.Add(otherSet);
                    }
                }

                aggregate.Add(ProcessNamedTest(pair.Key, fullSet));
            }
        }

        return aggregate.Select(ReduceSimpleResult).ToList();
    }
}

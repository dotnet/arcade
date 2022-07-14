# Test Reporting Queries

The following is a list of default queries to use to look up information about failed tests. Feel free to change them for your own usages. 

Click [here](TestReportingData.md) to learn more about the data we're collecting. 

Caveats (updated July 14, 2022): 
- Because this data is stored in an internal data source, it is unfortunately currently only available to Microsoft employees. (If you are an internal Microsoft employee, you can request access from the [.NET Engineering Services team](https://dev.azure.com/dnceng/internal/_wiki/wikis/DNCEng%20Services%20Wiki/107/How-to-get-a-hold-of-Engineering-Servicing).)

## Index
  - [Tests That Have Changed Failure Rate in the Last Week](#tests-that-have-changed-failure-rate-in-the-last-week)
  - [Tests That Have Failed X% of the Time in the Recent Timespan](#tests-that-have-failed-x-of-the-time-in-the-recent-timespan)
  - [Mean Value for the Expected Pass Rate for Tests](#mean-value-for-the-expected-pass-rate-for-tests)
  - [Mean Value for the Expected Pass on Retry Rate for Tests](#mean-value-for-the-expected-pass-on-retry-rate-for-tests)
  - [Build Analysis Reporting](#build-analysis-reporting)
  - [Sentiment Tracker Feedback](#sentiment-tracker-feedback)

## Tests That Have Changed Failure Rate in the Last Week

Variables: 
- `targetSignificance`: Target statisical likelihood that the failure change is due to a change in the last week
- `repo`: Repository to filter on. Set to empty string to inclue all repositories. Default is `dotnet/runtime`.
- `minimumHistoricalData`: Minimum number of historical data points to include to avoid new tests, (`0` includes all tests)
```
let targetSignificance = 0.95;
let repo = "dotnet/runtime";
let minimumHistoricalData = 0;
let dt = toscalar(AzureDevOpsTestAnalysis | summarize max(ReportDate));
AzureDevOpsTestAnalysis
| where ReportDate == dt and Repository == repo
| where Significance >= targetSignificance and CurrentFailCount != 0
| extend HistoricalTotal = HistoricalFailCount + HistoricalPassCount + HistoricalPassOnRerunCount
| where HistoricalTotal >= minimumHistoricalData
| order by Significance desc
```
:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA3WQy04CYQyF9/MUlRXERN24MGRMCMS4wyAvUGcqNPkvpO0oQ3h4O5AwYsZdc05P26+BDAxlQ/bOm8SfXGGqCEp4uHt6nBbBbaFddmFUZ0tk99Ik40ijsxk5cWziK6tl8XBYoGEXP9u1eW1Z3UAZzw6N0IK+ljtdk9osYWiVFY6gTYwofCCIuB+vfKOYT6LJZFr8kyqO8L0lIei7oSy7jZjqk6jsN7Wd2CFc+q9An8sh/G7CvBGhZC/IYZ6dGW6cyofQ3sjtnnidDYNj9kqfuf2lvqHqsLpMK/K3nszLmX8X+KWDz/ZAlpoEPtprtJq0+gHUohbl3wEAAA==) to query editor

## Tests That Have Failed X% of the Time in the Recent Timespan

This query will return a list of tests that have failed a certain percentage in the recent provided timespan. The default example in this query provides a list of tests in the dotnet/runtime repo that have failed 10% of the time in the last 7 days. 

Variables: 
- `ts`: [Kusto timespan format](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/timespan). Default is `7d`.
- `repo`: Repository to filter on. Set to empty string to inclue all repositories. Default is `dotnet/runtime`.
- `failureThreshold`: Double value denoting failure rate percentage. Default is `0.1` or 10%. 
- `excludeAlwaysFailing`: Set to true to filter out tests that are always failing to get a list of tests that are "flakey". Default is `true`.

```
let ts = 7d;                      // Timespan value
let repo = "dotnet/runtime";      // Optional: set to empty string if you want results from all repositories
let failureThreshold = 0.1;       // Double value denoting the lowest test fail rate to return
let excludeAlwaysFailing = true;  // Boolean. Set to true to exclude test results that are always failing
let subtable = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts) and iff(repo == "", Repository == Repository, Repository == repo)
| summarize numerator=sum(FailCount), denom=sum(PassCount+FailCount+PassOnRetryCount), asOfDate=max(ReportDate) by BuildDefinitionName, TestName, ArgumentHash;
let argumentHashMap = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts)
| summarize by ArgumentHash, Arguments;
subtable
| where denom > 0 and (todouble(numerator)/todouble(denom)) >= failureThreshold and iff(excludeAlwaysFailing, (todouble(numerator)/todouble(denom)) < 1, true)
| lookup (argumentHashMap) on ArgumentHash
| project BuildDefinitionName, TestName, Arguments, FailRate=(todouble(numerator) / todouble(denom)), FailCount=numerator, TotalRunCount=denom;
```

:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA51SzW7bMAy+5ymInGzUaNrTgHkukC4YdtkypHkBpqZjbbJkSFRTF3v4UTLitGkOxXwwbIr8/kRNDOyhgk91CRefxQK2qiPfo4En1IFmWmYc9Vam5rVlQ7xwwbA0zctpZt2zsgb1Z/CRwgJ1PQ/g2SmzB9XAYAMc0EQoH7RoaJztALVO2F6xdYp8ImtQ6eBo20pra3UtxDfXt+VJ4MqGnaZRHtRkLEcSbgm0PZAX/viKMOCQKcpxxMGZBE/PjzrUtNQHHPw3aYrDFbALVCb4e2s1obmGh9FKPEmWxsER/WiDW2RAR2Il4iVWAUxMPuwYo9IKli/iaEVP695vZdw/hK5DN8z+wqElmd5ICI5XUe1dBbi3Gfsc0NSSXZON8Uv+8yJ1priGWDn9nZ/EmVzwfWJSLwQmdCR5WFdJLYvOv1q5yLxIGXap+gu9T9Wr6fwq1tZmQ+yG4wD6dRPFVh0+ZyftOewGuA9K1ytqlFFxJ35iRwVE0+PX0u1Fh+Hv6NsyxYSvKj+w/9+03ngVHa+JTrS+nB2vZYJL9uEOblLgGds6LVg2BZYvplrqzfPI+25Pj9d1acOKD+J+gdsibVz0o639E3rIzhLKwZo39qS1d/Y3PfJH0/cFRGGbeIeXhMECzqWNE2kDqqlRsC2j3gQzHqTe8h/Tt/8tZwQAAA==) to query editor

## Mean Value for the Expected Pass Rate for Tests

This query will return a list of tests and the mean value for the expected pass rate based on historical data. (Comparably, an inverse to [Tests That Have Failed X% of the Time in the Recent Timespan](#tests-that-have-failed-x-of-the-time-in-the-recent-timespan))

Calculate the expected value (E[_Y_]) for how often a test is likely to pass on its initial run (retries not included) based on its historical data (e.g. monthly, weekly, daily aggregates). Ex: A test is known to fail once out of every seven runs. Its expected value (E[_Y_]) is determined to be 85.7%, meaning, we expect this test to succeed 85.7% of the time.

Variables: 
- `ts`: [Kusto timespan format](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/timespan). Default is `7d`.
- `repo`: Repository to filter on. Set to empty string to inclue all repositories. Default is `dotnet/runtime`.
- `excludeAlwaysPassing`: Set to true to filter out tests that are always passing. Default is `true`.

```
let ts = 7d;                      // Timespan value
let repo = "dotnet/runtime";      // Optional: set to empty string if you want results from all repositories
let excludeAlwaysPassing = true;  // Boolean. Set to true to exclude test results that are always passing
let subtable = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts) and iff(repo == "", Repository == Repository, Repository == repo)
| summarize numerator=sum(PassCount), denom=sum(PassCount+FailCount+PassOnRetryCount), asOfDate=max(ReportDate) by BuildDefinitionName, TestName, ArgumentHash;
let argumentHashMap = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts)
| summarize by ArgumentHash, Arguments;
subtable
| where denom > 0 and iff(excludeAlwaysPassing, (todouble(numerator)/todouble(denom)) < 1, true)
| lookup (argumentHashMap) on ArgumentHash
| project BuildDefinitionName, TestName, Arguments, MeanPassRate=(todouble(numerator) / todouble(denom)), PassCount=numerator, TotalRunCount=denom
| order by MeanPassRate;
```

:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA51U3WvbMBB/L/R/OAoDm5lkexgdzVJIP8b20GWkZTDGHi7xOdEiS0Y6NXXpH7+T3DhpycNYXqxI9/u4n84eDuH6oaEFUwk/UAeCyjr4jt7DDJnO4BL1ImhZAq8IaFt7n2qz618/f+cJsrIbsBWTAQQmz6A8aLUm3QJbaCKhNaDYgzKKFWpwwUDmiJ0iD8YKwix0KKnM4fhoOIQ5ehF6Bq2UZ+vUQnAlMkJGg+UAamt4pdsCNkTr+CxRiSAul46W4tnnA2nvDCa9pbWxGxMdVVIp5AsCG1icA92Ta8HLw0RrfgBfRTc5Ody1sJXE5Gpl5Ego5wQfPwxO3xRQExplltHXM1jSk/rkQip9WCxIQKk8isdsWdU0EEFNUuNhDKflCA7+xNKdFPsGTWepAzlqrMBOSsuGeChNRMqTUQ+aNqysQX0mbSYfVDcsPcsVmCWoClobYIMmUvmgxUTlbA2odeL2Kl4B+U6NHtJtTfQGWx8HJnKMgV2gUVK7sFZLDgO47cTiSRLtgF0YWyFeIQM6ErHIlwZGCDspH+aMc01CP3kMjq7oftr4O8H721DX6NrjoyfYrEjwMzHq+CoO7PlYJsFm7HNAU0p/VdZFJBmdFKkytdTGnd2/1ycRk0cBn7TUI4EJNTmUgrHsZbH5Sytp5zKAZGz9cvftZxm1bhX3pmYmM99uAeinVXQ7rvEh25nPYd7CRVC6vKIqvTHWfMOaCohtd6uJW4oPw1/Qr0ZdUri3dYPNfwf2sl2xsq+1U/Yiu72cHWPKAM7hXR/7oVkpIGNb2iDQrM8zH/Z7iSXP4RO8L9LsJFPa2nVoIHvVaB6/E/seY23j7J/46v1jjL6AG5nXaC9++saH7MEQXhssoL/pcV8o/JZRz4LpDlJt9GRdSS4Gui81+gtMm5LlhgUAAA==) to query editor

## Mean Value for the Expected Pass on Retry Rate for Tests

Retries are meant to unblock and prevent a build from failing due to failing test, but they are still indicative of unwanted behavior, therefore, we need to track how often a test passes when retries are introduced. Ex: A test has a 100% pass rate, but only when the test re-runs after a failure every six runs, so it’s expected value for re-runs is 83.3%.

Variables: 
- `ts`: [Kusto timespan format](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/timespan). Default is `14d`.
- `repo`: Repository to filter on. Set to empty string to inclue all repositories. Default is `dotnet/arcade`.

```
let ts = 14d;                      // Timespan value
let repo = "dotnet/arcade";        // Optional: set to empty string if you want results from all repositories
let subtable = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts) and iff(repo == "", Repository == Repository, Repository == repo) and PassOnRetryCount > 0
| summarize numerator=sum(PassOnRetryCount), denom=sum(FailCount+PassOnRetryCount), asOfDate=max(ReportDate) by BuildDefinitionName, TestName, ArgumentHash;
let argumentHashMap = AzureDevOpsTestsSummary
| where ReportDate >= ago(ts)
| summarize by ArgumentHash, Arguments;
subtable
| where denom > 0
| lookup (argumentHashMap) on ArgumentHash
| project BuildDefinitionName, TestName, Arguments, MeanPassOnRetryRate=(todouble(numerator) / todouble(denom)), PassOnRetryCount=numerator, TotalRunCount=denom
| order by MeanPassOnRetryRate;
```

:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA51Uy24TQRC8+ytakSKthYkThQOK5UiBgLgEI5MfaHt64yGzM6t52N6IA7/B7/ElqZkljmPlgPDJ24+q6ureHY9pLtFrCcReqBG2kaKjZBfGLe+JraLWy1oQZlokbRTV3jVUszba3pFKkuufHqOEOEIdQFbSFcwQtTGkrdJLjnot5GrAb0Akihay4rV2fkSD8Tj3eKmdlxFthKygANjRM5Ss3AadUSx0ZBZqOQTI3qwQ8nszaBu9U2kp6oQ+bS/oqi9fMdJ0dnp6XDrJc5ReqbOm62HA3+vIDV7e+mTRBFKP1jxiAj7M8B0FvaWcHlFwpOOfX78DybaVZZ5qzQa2YJAdiA70/vzk/PiEBkZgTqApnb1TE3r1Bwm3upHQsu2xSpOX1qHtSLloJY7ZL1nJ0WSvadZG7SybCwpS1ihNG6EV5mA3uqbOJcrOAyskAxVll4z9ZPCgo8s2FraQFpEXRsB49YC5r2U9a8MtnAnfU9Ow7wY/s2twZI5eH69hKF1Oie9cFcOwnI6u66qXDd1Ho1JZWLoceX46zOSeHuEbdjWz+Ua7jy5B+SWdgjgUCfoBV5IawS6dnyJWHZYPR6TEuqYkP2ODJfrmlTIOszqPMG14Wz1PNKRFRx/y3V9Lra3OBn/lBqeTrej/Xfk7iLDxC4fVpJjHe5Ebbv/XwxeDQsc+0TNtmAyelrWDK0P/9co4d59aqg5EDXH4LxBR2nr3Ayf8rwPj+m/wxdhzc54drKJTLkFOtVvOkPBWPUWLuCE8P1zDdFcPNhfZzJPtE6UFAp1XeBlhxSu8k0dFwXcxzQQAAA==) to query editor

## Search Failed Test Results as with Runfo

The `AzureDevOpsTests` table collects the details of failed test results, making it searchable as one would with [searching for tests with Runfo](https://runfo.azurewebsites.net/search/tests/#test). Here's a query to get you started: 

Variables: 

```
// NOTE: The following query uses equals operators. You may need to alter the query if you wish to use "contains" or "has" instead for any of the search values.
let started = 7d;                    // Timespan value to find test results after. 
let definition = "aspnetcore-ci";    // Optional: The name of the build definition, leave as empty string for all build definitions.
let reason = "BatchedCI";            // Optional: The Azure DevOps build reason value (e.g. PullRequest, BatchedCI, et cetera)
let targetBranch = "main";           // Optional: The name of the target branch the test is ran against.
let name = "";                       // Optional: The name of the test
let jobName = "";                    // Optional: The name of the job
let message = "";                    // Optional: Error message to search for
let workItemName = "";               // Optional: Work item name
AzureDevOpsTests
| where iff(definition == "", BuildDefinitionName == BuildDefinitionName, BuildDefinitionName == definition)
| where iff(reason == "", BuildReason == BuildReason, BuildReason == reason)
| where iff(targetBranch == "", Branch == Branch, Branch == targetBranch)
| where iff(name == "", TestName == TestName, TestName == name)
| where iff(message == "", Message == Message, Message == message)
| where iff(workItemName == "", WorkItemName == WorkItemName, WorkItemName == workItemName)
| where RunCompleted <= ago(started)
```

:part_alternation_mark: [Link](https://dataexplorer.azure.com/clusters/engsrvprod/databases/engineeringdata?query=H4sIAAAAAAAAA42UTY/aMBCG70j8h1FOINFwrFTKYb8Oe+hSIaRVj0MyIW4dO2s7oFT98R3HCZsElpaTPZ555uMdslzCy2b39AV2OUGmpdQnoQ7wVpGpobJkgd4qlBZ0SQadNjaGH7qCAmtQRCk4DSgdGXAMCGEig5pdTsLm/pkpECVaORTKRqANRDnygW+OMOWsBlDVoLOGYQlNksMRZUU2nk4kObAOjeNka/icruDKb7mEnSjIlqhCpE+cCcX1kXVgyFbSWcCMK40hQFNiB+GEVsyN0JaKXKINfUpEtGqhm9K/owzzUVhQV+a+EjLtMRYgCY8EyBMrSldzzcZPsulOygv/rjVDaEMF9+iSnNKH52g1am1Yxd3vyhA80nFT2hbbQkLnM4oPMXyvpNyydtz+As7oBXDKhHgKOA/5ebIHcvcGFQ+dqyhYpUEBN6cQomEfwhuLn7ewwBbAg5fctZ02cZwhuqrgPzMxN3B+6v3LTdRNDkcHDG+LxcP/Yp6MYSG7GF6udk1Z3oA7afPr2VHxYWkD3Ct7g2D3prbppNE0SLrjRu108gdOObHOIstm/U31aBbUy/54Noek62vmD33fofNhtm4je5m2Z1PvevEWAke04X61zPMtnPqWvv8IpdrKPcJPqeukOw+t3nsEOEseGN/er+1xYGu9R4yhzgH0OrL175evfUIPvq3Ugy5K3iX+0H1d859Hz9rv3vwvGtpprKYFAAA=) to query editor

## Build Analysis Reporting

This Power BI page contains the following reports: 
- Details of PRs outcomes when merged (e.g. when a PR was merged on red)
- Build outcomes and retry metrics
- Tests that pass on rerun

:part_alternation_mark: [Link](https://msit.powerbi.com/groups/me/reports/8bdd4339-ea39-4e67-963a-fc44a450605b/ReportSection?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47) to Power BI Report

## Sentiment Tracker Feedback

This report tracks the usage and trends of the feedback we receive via the sentiment tracker in the Build Analysis check on the PRs. 

:part_alternation_mark: [Link](https://msit.powerbi.com/groups/de8c4cb8-b06d-4af8-8609-3182bb4bdc7c/reports/e6deb422-46fc-4f80-8892-ba7036081986/ReportSection) to Power BI Report

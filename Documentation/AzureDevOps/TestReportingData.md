# Working with Test Reporting Data

## Where is the data? 

Kusto's `Engsrvprod/engineeringdata` cluster contains the following tables: 

- AzureDevOpsTests
- AzureDevOpsTestsHourly
- AzureDevOpsTestsSummary
- AzureDevOpsTestAnalysis

## What is the data? 

The data that we collect is only a subset of tests that run and not every test that runs. **We've chosen to collect results of tests that have failed in the last 90 days**. We assume that tests that are always passing or have continued to pass after 90 days are no longer interesting to look at, and thus, their results are no longer collected. 

### AzureDevOpsTests

This table contains the raw data that we collect for test results. Keep in mind that due to the large amount of data we are collecting, we are not including initial passing results in the raw table. However, you can see aggregates of passing results in other tables. 

Notable fields in this table include: 

**QueueName**: The OS queue the test ran on. Useful when needing to track down a configuration a test ran on. 
**Branch**: Name of the code branch the test ran from.
**Outcome**: Contains values of `Failed`, `PassedOnRerun`, and `NotExecuted`.

### AzureDevOpsTestsHourly

Due to the amount of data that we need to process, we start with hourly aggregations of test results. The data collected on this table is for all failed tests in the last 90 days that we are tracking, so it may occur that we have a row for a test that has not ran in the last hour, but has failed in the last 90 days. 

Because this table is an aggregate, you'll notice that some of the fields, such as `QueueName` are not available going forward. 

Notable fields in this table include:

**Branch**: Name of the code branch the test ran from. 
**PassCount**: How many times the test passed on the first try in the last hour.
**FailCount**: How many times the test failed in the last hour. 
**PassOnRetryCount**: How many times the test passed after a retry in the last hour. 

### AzureDevOpsTestsSummary

Daily, we aggregate the hourly values into this table. It contains nearly the same fields as the `AzureDevOpsTestsHourly` table does, with the exception of fields we track for debugging purposes. 

### AzureDevOpsTestAnalysis

This table contains the results of running the AzureDevOpsTestSummary results through [Chi-Squared Distribution](https://en.wikipedia.org/wiki/Chi-squared_distribution) in order to get a statistical analysis of the failures. 

Notable fields in this table include:

**Significance**: The closer this value is to 1, the more likely something changed recently to cause this test to start failing. 
**SplitDate**: The date of the first day of the week of data aggregated. 

## How do we collect the data? 

Only tests that are ran through Helix have their results captured to be added to the test reporting tables. This means that tests that run in pipelines not utilized by pull requests (e.g. CI pipelines) will have their data collected as well. And as long as our PAT has access to the Azure DevOps project the test runs in, we can collect results from tests that run there. 

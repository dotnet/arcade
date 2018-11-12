# .NET Core 3 Engineering transition plan (dotnet/coreclr)

## Primary Deliverable Breakdown of remaining work

### Internal builds from dnceng (overlaps dependency flow)
|Task         |Estimate (days)    |Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| decide what to do about native dependencies in official build|3|40|X||100%|
| add signing to official product builds|3|60|X|||
| add red hat product build job|2|50|X|||
| add linux-musl product build job|2|50|X|||
| publish packages to blob feed (this is part of dependency flow requirement)|0.5|70|X|||
| publish symbols|1|70|X|||
| update Build Assets Registry (this is part of dependency flow requirement)|0.5|60|X|||
| validate output against buildpipeline official build|1|80|X|||
| turn off buildpipeline official build and remove old scripts|1|80||X||
|**Total**    |**14**                 |                    |13|1||

### Engineering dependency flow (beyond what required for official build)
|Task         |Estimate (days)    |Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| set up branches/channels/subscriptions|2|20|X|||
| express package dependencies in new format|8|20|X|||
|**Total**    |**10**                 |                    |10|||

### Using Azure DevOps for CI
|Task         |Estimate (days)    |Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| express test matrix with commented jobs for those I haven't implemented|1|90|X||100%|
| add formatting jobs|1|50|X|||
| add IBC collection and consumption|4|30||X||
| add R2R jobs|1|60||X||
| add perf jobs|4|30||X||
| add superpmi jobs|3|30||X||
| run various test legs at job queue time|1|60|X|||
| validate mission control reporting for xunit results with different test legs|1|50|X|||
| set up a way to trigger certain test legs from github PRs|2|30|X|||
|**Total**    |**18**                 |-                    |6|12||

### Using shared toolset (Arcade SDK)
|Task         |Estimate (days)    |Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| conform to their directory structure for build output (not sure we should do this)|8|20||X||
| factor our build to go through the arcade entry point project (not sure we should do this) |8|20||X||
| use arcade conventions for package directories and MSBuild imports|2|40|X|||
| migrate our tests to SDK-style projects|6|30|X|||
| remove config.json and run.exe from our build scripts (easiest once buildpipeline no longer uses them)|4|40|X|||
|**Total**    |**28**                 |-                    |12|16||

## Delivery dates
|Priority |Expected Delivery|
|---|---|
|1| 12/14/2018 |
|2| 03/15/2019|

# .NET Core 3 Engineering transition plan (dotnet/coreclr)

## Overall status
We are on track to have Official Builds working in AzDO and participating in Dependency Flow by
Dec 12.  The new Azure DevOps (AzDO) CI will be based on the Official Build logic and is therefore
waiting for that to complete.  Full transition off Jenkins will not happen until late Jan at the
earliest but you should expect to see some new badges on PRs as AzDO begins to take over for Jenkins.
We've prototyped how tests will run in Helix and what the PR-triggered test experience will look
like--including how test results will be viewed at the AzDO portal!

## Primary Deliverable Breakdown of remaining work

### Internal builds from dnceng (overlaps dependency flow)
Except for a couple of issues blocking [ARM](https://github.com/dotnet/core-eng/issues/4764) and
[Alpine/musl](https://dnceng.visualstudio.com/internal/_workitems/edit/109) builds, we have worked
out how the build CoreCLR for every supported platform.  Signing has a few kinks to work out, but is
almost done.  The big things to figure out are publishing the packages and updating the Build Asset
Registry to we participate in Dependency Flow, and we are on track to finish that by Dec 14th.

|Task         |Estimate remaining (days)|Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| Decide what to do about native dependencies in official build|0|-|X| |X|
| Add signing to official product builds|0.5|90|X| |(Working through the last set of issues on this)|
| Add red hat product build job|0|-|X| |X|
| Add linux-musl product build job|0|-|X| |X|
| Publish packages to blob feed (this is part of dependency flow requirement)|1|50|X| |Need to add step to YAML to build packages and upload|
| Publish symbols|1|50|X| | |
| Update Build Assets Registry (this is part of dependency flow requirement)|0.5|60|X| | |
| IBC and PGO data|3|30| |X||
| Validate output against buildpipeline official build|1|80| |X| |
| Turn off buildpipeline official build and remove old scripts|1|80| |X| |
|**Total**    |**8**                 | | | | |

### Engineering dependency flow (beyond what required for official build)
These Dependency Flow tasks are done, though they have only been lightly tested.  We won't be able
to tell whether everything comes together until the Official Builds are published and we try to 
enable Dependency Flow end to end.

|Task         |Estimate remaining (days)|Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| Set up branches/channels/subscriptions|0|-|X| |X|
| Express package dependencies in new format|0|-|X| |X|
|**Total**    |**0**                 | | | | |

### Using Azure DevOps for CI
Unlike the existing Jenkins system, the AzDO-based CI system will *heavily* leverage Official Build logic,
which should reduce the number of build breaks that slip through the CI.  It will also distribute testing
in Helix like the old "PipeBuild" Official Build works.  The CI is in some ways easier than the Official
Build because it doesn't need to do Signing or Publishing, but it does need to support a more diverse set
of test modes.  We've been prototyping how we thing PRs and commits will be tested, and we think we have
a reasonable compromise between what we had in Jenkins and what works reasonably well in AzDO.  We plan to
define a limited set of "test pipelines" that represent "interesting" scenarios that some PRs might need to
opt into.  A new feature in AzDO called
[Workload Selection](https://dev.azure.com/dnceng/internal/_workitems/edit/48) should allow us to request
additional testing similar to how we interact with @dotnet-bot on GitHub today.  We've also been playing
with how test results can be viewed in the AzDO portal and are following up with our Engineering and the
AzDO teams.

We won't have all test jobs ported to AzDO by Dec 14, but we plan to be off of Jenkins by early next year.
Jobs like CoreFX testing and SuperPMI will likely have to be re-implemented from scratch.  Note that we
may also have to port the release/2.1 and release/2.2 branches to the new AzDO system as well, and that
work is TBD.

|Task         |Estimate remaining (days)|Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| Express test matrix with commented jobs for those not implemented|0|-|X| |X|
| Group test jobs into useful subsets--each with its own pipeline|0|-|X| |X|
| Add Formatting jobs|1|80| |X| |
| Add IBC collection and consumption|4|30| |X| |
| Add R2R jobs|1|80| |X| |
| Add Perf Jobs|4|30| |X| |
| Add SuperPMI jobs|3|50| |X| |
| Run various test legs at job queue time|1|80|X| | |
| Validate ~~mission control~~AzDO reporting for xunit results with different test legs|0|-|X| |X|
| Set up a way to trigger certain test legs from github PRs|0|-|X| |X|
|**Total**    |**14**                 | | | | |

### Using shared toolset (Arcade SDK)
CoreCLR made a conscious decision to deprioritize the Arcade transition in favor of getting the Official
Build and CI working in the new system ASAP.  Keeping the Product Construction effort unblocked is our
top priority.  We have accrued a lot of debt because of this and will need to clean it up at the same
time we are bringing the rest of the test jobs over.

|Task         |Estimate remaining (days)|Estimate confidence (%)  |Priority 1 |Priority 2| Done |
|-------------|-------------------|---------------------|---|---|---|
| Conform to their directory structure for build output (not sure we should do this)|8|20| |X| |
| Factor our build to go through the arcade entry point project (not sure we should do this) |8|20| |X| |
| Use arcade conventions for package directories and MSBuild imports|2|40| |X| |
| Migrate our tests to SDK-style projects|6|30| |X| |
| Remove config.json and run.exe from our build scripts (easiest once buildpipeline no longer uses them)|4|40| |X| |
|**Total**    |**28**                 | | | | |

## Delivery dates
|Priority |Expected Delivery|
|---|---|
|1| 12/14/2018 |
|2| 03/15/2019|

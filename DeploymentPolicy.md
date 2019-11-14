# Deployment Principles

-	The rollout updates and impact are known beforehand by our customers
-   Deployment quality shows a continual, positive trend over time
-	Key metrics are measured for each rollout and displayed on a dashboard ([Rollout Score Card](../Rollout-Scorecards/RolloutScoring.md)), and a clear rollback threshold is defined.
-	Deploying multiple small updates is much preferable to single large updates.
-	No breaking changes ([exceptions granted via policy](https://github.com/dotnet/arcade/blob/master/Documentation/Policy/ChangesPolicy.md))
-	Rollouts themselves are frictionless, automated, and meet deployment quality goals
-   Rollbacks (when triggered by clear thresholds) are also frictionless, automated, and meet quality goals
-	Scenario tests are automated, accurately represent our customers, run prior to every deployment, and block deployment on failure
-	All ad-hoc/exploratory testing is done by the dev (not as part of the rollout)
-	Staging accurately represents production can be used for testing with confidence
-   Staging is always green

It is important that we have metrics, with clear targets to measure how we're doing against these principles over time.

# Policy

Deployments follow the pattern described in the [Deployment Process](../Validation/DeploymentProcess.md).

## Rollbacks
* Rollbacks are triggered by manually kicking off a build/release pipeline to deploy the *n-1*th version. There should be minimal to no human intervention needed in rolling back a deployment. Every new deployable service/feature that is coming up needs to have a rollback story defined and implemented as part of the epic.
    - For Pipeline-based deployment, revert the change to production branch and kick-off a build and deploy pipeline.
    - For non-yaml based release pipelines, rollout the previous release.
    - Rolling back database changes can be tricky especially if data loss is incurred like in case of a added column/dropping a table. This is only scenario where they might be manual intervention needed for rollback.
* Workflow for Rollback:
    - When a service in production is discovered to be in a "downed" state (e.g. not taking work, constantly throwing errors, etc.) following a rollout, then rollback IMMEDIATELY to a previously known working deployment. Continue investigation in staging by reproing the failure.
    - Rollback PRs need to have tags in title indicating its a rollback like "[ROLLBACK] blah blah"
    - Create a GitHub issue for tracking, with tags indicating that this is a rollback.
    - Communicate the state of affairs to dncpartners@microsoft.com when the issue is identified along with the tracking GitHub issue, and when it's mitigated.

## Hotfixes
* Hotfixes are reactive rollouts done in response to an error in production due to the rollout that just occurred. There are 2 different ways to do this:
    - Make a PR to the Production branch -> rollout to Prod environment -> Make a PR to merge the change back to master.
    - Make a PR to the Staging / master branch -> rollout to staging environment -> test the change -> make a PR to merge the change from master to production branch -> rollout to Prod environment. (This can be done only if staging / master is on lockdown)
    - Manual changes to Prod environment - data changes in the DB, on-prem machine settings etc.
* Workflow for Hotfix:
    - When a service in production is discovered to be in a erroneous state following a rollout.
    - Hotfix PRs need to have tags in title indicating its a hotfix like "[HOTFIX] blah blah"
    - Create a GitHub issue for tracking, with tags indicating that this a hotfix.
    - If the root cause of failure is determined, make the hotfix and deploy to prod.
    - Communicate the state of affairs to dncpartners@microsoft.com when the issue is identified along with the tracking GitHub issue, and when it's mitigated.
    - The hotfix needs to be communicated to the team and approved by management.

## Unit Of Deployment
* All services that are dependent on each other to rollout, are considered a unit of deployment and need to be deployed together in a pipeline.
* Each service in the unit can be in a separate stage for the ease of rerunning a specific service's deployment.
* Deployments to Prod must be approved.
* Any service that is not connected to other services needs to be in a separate pipeline for deployment, in other words is considered a separate unit.

## Staging always green
* The staging environment should accurately reflect prod. In other words, if a rollout would break prod it should break staging too.
* Unit/Functional tests will run on staging on every PR Build and block PR merges if tests fail.
* Every service should have post-deployment checks/scenario tests to ensure the services are up and running taking work. If post-deployment checks/tests fail, changes need to be reverted/fixed via PR in Staging to ensure the deployed services in staging are not broken.
* All exploratory / experimental changes need to be done on a dev branch. Any change that is known/expected to temproraily break staging, needs to be communicated to the team (dotnetes@microsoft.com) well in advance and be reasonably timeboxed (case-by-case). This communication should be sent out explaining the period of time in which we expect staging will be down/broken, and put back into a working state by kicking off a build/deploy from master at the appropriate time.
* All merges to staging/master need to stop two days prior to the day of rollout for e.g. if the rollout is happening on a Wednesday, the last merge to staging/master should happen on EOD Sunday giving us 2 full days for stabilization. Only changes that unblock the staging build would go into staging/master during the stabilization period.

# Deployment Principles

-	The rollout updates and impact are known beforehand by our customers
-   Deployment quality shows a continual, positive trend over time
-	Key metrics are measured for each rollout and displayed on a dashboard, and a clear rollback threshold is defined.
-	Deploying multiple small updates is much preferable to single large updates.
-	No breaking changes ([exceptions granted via policy](https://github.com/dotnet/arcade/blob/master/Documentation/Policy/ChangesPolicy.md))
-	Rollouts themselves are frictionless, automated, and meet deployment quality goals
-   Rollbacks (when triggered by clear thresholds) are also frictionless, automated, and meet quality goals
-	Scenario tests are automated, accurately represent our customers, run prior to every deployment, and block deployment on failure
-	All ad-hoc/exploratory testing is done by the dev (not as part of the rollout)
-	Staging accurately represents production can be used for testing with confidence

It is important that we have metrics, with clear targets to measure how we're doing against these principles over time.

# Dependency flow automation => Maestro ++
Maestro++ is an automation of the work a Dev would do to flow dependencies across repos using [Darc](darc doc).

## Requirements:
- Triggers:
    - Package publish
    - Time schedule task
    - Requested in a PR (i.e. @dotnet-bot test product build) by following a speculative product build.
    - Manual trigger
- It must work for public and private builds/repos
- Manage channels subscriptions for each repo, where it will identify:
    - What events/files to listen to.
    - How often to start a build and start flowing dependencies from that point.
- It must have the option to auto-merge PRs when all the checks are green.
- Notify the result of the build triggered by maestro++. Did the target repo get the update? Does the source repo knows if the children were updated?

## Considerations:
- It must manage speculative version flow (Once the product build is done, in parallel execute tests and queue product builds for the dependents).
- It must manage speculative product build (Dev test build. Flow dependencies until the last dependent validating build and option to not consider test results).
- Secret management

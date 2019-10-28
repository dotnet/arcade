# Team Process Policy

- When a service in production is discovered to be in a "downed" state (e.g. not taking work, constantly throwing errors, et cetera) following a rollout, immediate rollback to a previously known working deployment. 
- Timebox efforts to reproduce the above failing service in staging. The purpose of timeboxing is to let the team know a time in which the staging environment might not be operating correctly while the reproduction efforts are ongoing. Determining what the timebox will be will be left up to the developers working on the troubleshooting. 
- For all work done, a GitHub issue must exist, this includes, but not limited to: 
  - Every pull request should reference back to a GitHub issue (if the PR is in AzDO, include a link to the GitHub issue in the PR details).
  - Every rollout must have a GitHub issue to document the change to production.
  - Every hotfix and deployment rollback must have a GitHub issue to document the change to production. 
- All dependency additions/changes, such as version upgrades must be approved through team management (e.g. Application Insights)
- The team must be involved with decisions to hotfix or add functionality at the last minute, and receive sign-off from management. 
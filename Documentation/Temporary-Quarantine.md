# Temporary Quarantine Procedure
In order to allow .NET development to proceed, problems in the build or test that cannot be resolved immediately need a mechanism
to quarantine them in order to unblock the developer workflow of the many developers that contribute to the product.

Quarantine is not the first resort, but it is a tool in order to ensure successful building of the product.

In general if a build or test fails, the steps should be as follows.
1. If the source PR/change can be identified, it should be backed out to restore correct behavior, and then the correction made in a future PR to reinstate the new code
2. If the problem can be definitively fixed quickly, it should be done as soon as possible
3. **If the broken component can be isolated to be removed from PR's and CI builds, it should be quarantined**
4. If none of the above are possible, is should be priority 0 to tackle the situation to get the build unblocked as soon as possible

Step 3 is the focus of this proposal.

## When is something "broken"
We are going to consider something "broken" an in need of remediation __**if it has failed 3 of the last 10 builds in the CI pipeline**__.
The CI pipeline should be green 100% of the time, so 3 fails indicates that something needs to be done.

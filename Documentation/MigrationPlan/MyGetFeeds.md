# (DRAFT) Plan to discontinue our use of MyGet

## Summary
Now that Azure DevOps has feature parity with MyGet (namely public feeds), and we've already taken our primary dependency on Azure DevOps feeds, it's time to completely discontinue our use of MyGet.

## Plan Overview
1. Broadly socialize this plan with product teams to both set expectations, and catch any gaps.  (end of Jan 2020)
2. Make all MyGet feeds read-only (first part of Feb)
3. Bulk load a new ‘legacy’ feed in Azure DevOps from the existing MyGet feeds (end of Feb)
4. Turn off MyGet (first week of March)
5. Finish cleaning up (April)

## Details

### Plan Socialization
- The scope include **all** use of MyGet, including servicing.  It is also expected that there are a variety of places we're currently unaware of...
- There likely will need to be some communication to our community about this change.
- This migration will likely be fairly disruptive, so setting clear expectations, and giving plenty of time to adjust is a key to our success.
- There are a variety of partner teams which depend on MyGet today to get updates to .NET Core.  These consumers need to be identified as part of the socialization effort so that they can move their dependencies as well.

### Make MyGet Feeds Read-Only
- This stops the publishing of new packages which gives a stable "snapshot" to migrate.
- Before this happens, it's very important that partner teams are aware that new packages will only be published to the Azure DevOps feeds.  
- There will likely be some fallout from this step.  Once it's done however, our confidence will be higher that we have functional understanding of who the consumers of our feeds are.

### Bulk Load a "Legacy" Feed
- MyGet is backed by our own Azure storage location.  The idea is to bulk load a new "legacy" feed in Azure DevOps with all of our existing packages in MyGet.
- This should allow our servicing builds to simply add a new feed to their nuget.config.
- It's very important however, that no new packages be published to this legacy feed.  The eventual idea is that we can simply "turn off" the legacy feed.

### Turn off MyGet (and clean up)
- The two main factors which should make this transition reasonable are:
  - We don't have that many dependencies on MyGet left in our 3.x builds (relatively speaking)
  - There will be a "legacy" feed for existing packages.
  - The migration is being staged over time
- Regardless, it is understood that this transition wil be noisy and the engineering servicing team is committed to doing whatever is necessary to helping out where needed. 
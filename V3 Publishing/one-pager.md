## V3 Publishing 
We need to retire V1 and V2 publishing. Currently arcade release/5.0, master and all the repos getting updates from these branches are already using V3 publishing. In this epic we are planning to move arcade release/3.0 branch to use V3 publishing. We need all the repos currently which takes updates from arcade release/3.0 to use the latest V3 publishing. Also removing all the legacy publishing code from arcade master and release/3.0 branches. 

## Stakeholders
- .NET Core Engineering
- .NET Core Engingeering Partners

## Risk
- Arcade-services might break, when we start on-boarding to V3, beacuse arcade-services currently still relies on older and newer versions of arcade. 

## Rollout and Deployment
- We are deprecating legacy publshing code.
- Need customer repos to on-board to V3 publishing. Will create a list of all the repos and help them on-board. Documentation on how to on-baord is already in place. 

## Serviceability
Already have test written for publishing using V3 infrastructure for arcade release/5.0 and master branches.

## Monitoring
- Build failure email notification is set for all the customer repos.

## FR Hand off
Publishing FAQs are already in place in Publishing.md, this document can be updated incase of new errors. 
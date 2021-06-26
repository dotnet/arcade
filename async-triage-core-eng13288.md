# Asynchronous Triage

We want to implement a mechanism that will allow newly-created issues on GitHub that still need to be triaged to automatically have their own dedicated conversation started on Teams. The primary motivation for this project is to make triage more conducive to a hybrid working environment. For example, participants who are not able to attend live meetings can easily see which issues were recently triaged and those still in progress by viewing the generated conversation thread. Additionally, we hope that increased accessibility on Teams will empower more people to participate in the triage process. The discussions surrounding triage will integrate into Teams in a dedicated channel, but will mostly reflect the GitHub issue discussion thread. However, it may also be easier to have longer discussions in Teams-only to avoid clogging the initial GitHub issue (and to simply include a summary of decisions for context on GitHub). It would also be useful to denote conversations as [Triaged] or [Needs Triage] in Teams for clarity.

See Epic for more context: https://github.com/dotnet/core-eng/issues/13288

## Stakeholders
  - .NET Engineering Services team

## Risk

### Unknowns and Open Questions
- How can we distinguish if an issue needs triage, or if it was just created and will immediately have an Epic assigned to it? ([link to issue](https://github.com/dotnet/core-eng/issues/13457))
  - Suggested - a background job that occasionally scans for unassigned issues and adds a conversation on Teams if necessary. This also addresses an interesting case in which an issue was assigned to one Epic, then unassigned.
- How can we push stagnating issues to the top of discussion on Teams so they can be resolved? What do we define as a stale issue?
  - Suggested - have a bot periodically comment on the conversations that still need resolution.
  - Suggested - weekly generated conversation listing all of the issues that still need to be triaged.
- How much conversation that happens only on Teams will be reflected on GitHub?
- We don't know how many people will actually choose to participate on the Teams conversation, or will continue to use the GitHub issue thread.
- Some issues may not need to be assigned to an Epic (ex. customers that open issues for themselves).

### Proof of Concepts
- Verify that we are able to open a new conversation in a channel on Teams when a new issue is opened on GitHub. Try to utilize some similar functionality to the FR Mention Bot.
- Verify that additional comments added to an issue update their dedicated Teams conversation.
- Verify that existing Teams conversations can be updated when triaged, such as denoting [Triaged] in the conversation title.
- Verify that we can grab all the existing unassigned issues from GitHub (this will require the use of the ZenHub API to check if it is in an Epic).

### Dependencies
- GitHub API
- ZenHub API
- Teams

Additionally, the solution should be a new addition. Note that most of the GitHub webhook functionality is already implemented in Arcade Services. The goal to have the work completed by is September 10th at the latest.

## Serviceability

### Testing
To avoid sending actual requests to Teams and cluttering up the channel, we can test some functionality of the code by creating mock HTTP calls and checking if they send requests in the right cases. We can also try creating a hidden channel on Teams for integration testing.

### Security
Identifying secrets that will be used include the Teams channel connector URI and authentication for the GitHub app. Note that all PII is owned by GitHub and Teams.
- Steps for setting up repro/test/dev environments? (not too sure about this question)

### Rollout and Deployment
- The solution will be most likely be deployed on Arcade Services, and thus on the Arcade Services cadence. Once deployed, check the Async Triage channel on Teams for updates.
- While nothing is being deprecated, we will probably remove the "Needs Triage" and "I Think This is Triaged" bot tags once the solution is deployed.
- What are the risks during deployment?
  - If the solution stops working, new untriaged issues may go unnoticed without a conversation being created on Teams. It may require some monitoring to make sure that untriaged issues are consistently being add on Teams.

## Usage Telemetry
Suggestions for tracking "usefulness" of the solution include measuring the average amount of time it takes for an issue to be triaged, how many people are involved, and the average length of conversations. However, more discussion is needed to figure out how the necessary information will be collected.

## Monitoring
- Is there existing monitoring that will be used by this epic?
  - On the Teams end, we can monitor if there are an abnormal amount of non-successful requests made to Teams.
  - Suggested - using Azure Application Insights to monitor the functionality of the GitHub controller.
- If new monitoring is needed, it should be defined and alerting thresholds should be set up.

## FR Hand off
- We should create some documentation on what functionalities the solution currently possesses and link to the code for more information. The code should also be clearly documented.
- If you have created new monitoring rules - what tools/processes should FR use to troubleshoot alerts
- If existing monitoring is used, do the parameters need to be updated to accommodate these new updates

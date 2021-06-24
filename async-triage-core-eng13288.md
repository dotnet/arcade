# Asynchronous Triage

We want to implement a mechanism that will allow newly-created issues on GitHub that still need to be triaged to automatically have their own dedicated conversation started on Teams. The primary motivation for this project is to make triage more conducive to a hybrid working environment. For example, participants who are not able to attend live meetings can easily see which issues were recently triaged and those still in progress by viewing the generated conversation thread. Additionally, we hope that increased accessibility on Teams will empower more people to participate in the triage process. The discussions surrounding triage will integrate into Teams in a dedicated channel, but will mostly reflect the GitHub issue discussion thread. However, it may also be easier to have longer discussions in Teams-only to avoid clogging the initial GitHub issue (and to simply include a summary of decisions for context on GitHub). It would also be useful to denote conversations as [Triaged] or [Needs Triage] in Teams for clarity.

See Epic for more context: https://github.com/dotnet/core-eng/issues/13288

### Stakeholders

- Who are the stakeholders (e.g. folks who will be using this feature; folks who are requesting this work; folks who need to "sign-off" on the epic)
  - .NET Engineering Services triage team

### Risk

- What are the unknowns? (Mine are more like open questions, not potential risks)
  - How can we distinguish if an issue needs triage, or if it was just created and will immediately have an Epic assigned to it? ([link to issue](https://github.com/dotnet/core-eng/issues/13457))
  - How to close/archive a conversation once an issue has been assigned to an Epic? What if the conversation needs to be re-opened?
  - How can we "push" stagnating issues to the top of discussion on Teams so they can be resolved?
  - How much back-and-forth will be reflected on GitHub? Can an Epic be assigned from Teams?
  - We don't know how many people will actually choose to participate on the Teams conversation, or will continue to use the GitHub issue thread.
- Are there any POCs (proof of concepts) required to be built for this epic?
  - Through a proof of concept, verify that we are able to open a new conversation in a channel on Teams when a new issue is opened on GitHub. Try to utilize some similar functionality to the FR Mention Bot.
  - Through a proof of concept, verify that additional comments added to an issue update their dedicated Teams conversation.
- What dependencies will this epic have? Are the dependencies currently in a state that the functionality in the epic can consume them now, or will they need to be updated?
  - GitHub API, ZenHub API, Teams
- Will the new implementation of any existing functionality cause breaking changes for existing consumers?
  - No, this should be a new addition. Note that most of the GitHub webhook functionality is already implemented/deployed in Arcade Services.
- Is there a goal to have this work completed by, and what is the risk of not hitting that date? (e.g. missed OKRs, increased pain-points for consumers, functionality is required for the next product release, et cetera)
  - All work should be completed within 12 weeks (September 10th at the latest).

### Serviceability

- How will the components that make up this epic be tested?
  - Functional testing of the bot - sending fake HTTP requests to Teams and inspecting the response?
- How will we have confidence in the deployments/shipping of the components of this epic?
- Identifying secrets (e.g. PATs, certificates, et cetera) that will be used (new ones to be created; existing ones to be used).
    - Instructions for rotating secret (if the secret is new)
    - Not sure if this counts as a secret: Teams channel connector URI, GitHub authentication
- Does this change any existing SDL threat or data privacy models? (models can be found in [sharepoint](https://microsoft.sharepoint.com/teams/netfx/engineering/Shared%20Documents/Forms/AllItems.aspx?FolderCTID=0x01200053A84D1D9752264EB84A423D43EE2F05&viewid=6e9ff2b3%2D49b8%2D468b%2Db0d3%2Db1652e0bbdd3&id=%2Fteams%2Fnetfx%2Fengineering%2FShared%20Documents%2FSecurity%20Docs) folder)
- Does this require a new SDL threat or data privacy models?
- Steps for setting up repro/test/dev environments?

#### Rollout and Deployment
- How will we roll this out safely into production?
  - Are we deprecating something else? No
- How often and with what means we will deploy this?
  - One-time deployment?
- What needs to be deployed and where?
  - Deployment in Arcade Services repo, check the Async Triage channel on Teams for updates.
- What are the risks when doing it?
- What are the dependencies when rolling out?

### Usage Telemetry
- How are we tracking the “usefulness” to our customers of the goals?
  - Perhaps we can measure the average amount of time it takes for an issue to be triaged, how many people are involved, the average length of conversations, etc.
- How are we tracking the usage of the changes of the goals?

### Monitoring
- Is there existing monitoring that will be used by this epic?
- If new monitoring is needed, it should be defined and alerting thresholds should be set up.

### FR Hand off
- What documentation/information needs to be provided to FR so the team as a whole is successful in maintaining these changes?
  - Create some documentation on what functionalities the bot currently possesses and link to the code for more information.
  - Code should also be clearly documented.
- If you have created new monitoring rules - what tools/processes should FR use to troubleshoot alerts
- If existing monitoring is used, do the parameters need to be updated to accommodate these new updates

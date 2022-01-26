# .NET Core Engineering Services Operations info: - Lifecycle management for Helix Queues and .NET Core Engineering Build Images

## Process Details

### Glossary of terms:

- **End of life (EOL)**: The date at which an operating system is no longer supported by its publisher. Publishers will often provide more than one timeline for this (often charging customers money for longer terms). For .NET Core's purposes, this is usally the longest possible period where security vulnerabilities will be addressed as this is what customers running .NET on these operating systems expect, as we will need to be able to continue testing the shipped product on these operating systems until the final days of their support lifecycle.

- **Estimated Removal Date (ERD)**: The date that .NET Core Engineering Services intends to remove a test queue or build image. This date is meant to force conversations to be had and actions to be taken, and is not meant to indicate a customer promise of .NET Core's OS support.  The date may be before, or after the EOL date (preferably before) at DncEng's discretion. With the exception of the dotnet-helix-machines build where it can become an error once elapsed, this time is only used to inform warnings to users. Estimated removal dates thus can be arbitrarily extended (leaving history behind in Git commits) with sufficient cause, but the goal is to never have unsupported and un-patch-able operating systems managed by the team.

- **Matrix of Truth**: ([Epic issue link](https://github.com/dotnet/core-eng/issues/11077)) Ongoing work to provide a single source of information for operating system test matrix and life cycle for .NET Core Engineering systems.

- **Helix Queue**: Set of machines, whether they are an Azure VM Scaleset or physical machines, which execute test work items.

- **Build image**: Azure Compute Gallery image used to populate 1ES Pool provider instances used for all .NET Core builds. Due to executive order, we must perform all builds through images pushed to this system.

### Summary:

#### Why does this "process" exist in the first place?

Previously, removing old Helix queues and images has been a best-effort process. This has several major problems including:

- It causes us to continue using images that no longer receive security patching, leading to potential attacks as well as definitely causing monitoring applications to detect these machines and cause us to react to this.
- Helix VM and on-prem machine capacity is divided amongst whatever capacity we have.  If we keep around old Helix queues (on-prem or in Azure), this limits the ability to provide this capacity in still-supported OSes.
- Users would frequently only find out on rollout that their queue/image had been removed.

#### Who benefits? 
Helix users benefit from secure, patched machines and a regular communication of when what they use will be deprecated. The .NET Core product teams get the most secure and accurate-to-real-users'-machines images (that is, containing all the recent patches and updates to operating systems and components which could affect test / product behaviors) possible to run their tests or builds on.

#### What happens if it doesn't happen?
Without a regular process, we will be bogged down with responding to alerts for VMs or other resources using 'old' images, and even ignoring any security-related implications (reasonable for most Helix test machines, less so for build images), we will continue to pay for storage and compute costs for no-longer-supported operating systems.

### Process Boundaries

- Related repositories:
   - https://dnceng.visualstudio.com/internal/_git/dotnet-helix-machines --> Source control for all Helix VMs, 1ES Pool provider images, and Helix on-premises machine setup functionality
   - https://github.com/dotnet/dotnet-buildtools-prereqs-docker --> Source control for a wide variety of docker images automatedly published to Microsoft Container Registry (MCR). Images used for testing Helix "docker" images come from here. Work will eventually be done to move most of this image generation into the dotnet-helix-machines repo.
- Task scope:

  - In scope:
    - Monitor [dotnet-helix-machines](https://dnceng.visualstudio.com/internal/_build?definitionId=596) pipeline and respond to warnings / errors produced by EstimatedRemovalDate time elapsing by either extending this date or removing these queues.
    - Whenever extending a date further out than the lifespan of an existing operating system, comments should be added above this (or at least in commit messages) explaining the extension.
    - Coordinate with rollout owners to ensure that upcoming removals (any queue that has produced a warning in the dotnet-helix-machines "Validate" stage) are included in rollout status emails. 
    - Until all values have data, periodically (monthly?) review and update the EstimatedRemovalDate of Helix queues in dotnet-helix-machines repository for accuracy and existence (eventually every OS we may deprecate should have some value for end of life). Once all Helix queues have some date (based off the next Wednesday after the OS is either end-of-life, or end-of-.NET-special-support) we may automate this by making the property mandatory.  Use .NET release PM team and the internet for deciding dates.

  - Not in scope:
    - Deciding the OSes for which we will provide images. (tracked by https://github.com/dotnet/core-eng/issues/11077)
    - Patching of Operating systems or updating of artifacts (tracked by https://github.com/dotnet/core-eng/issues/14605)

- Contacts for non-owned parts of the process: For external ownership, who can we talk to?
  - For end-of-life operating systems, the release PM team owns the final word of when we can actually get rid of support. Certain operating systems can and will be maintained outside their publicly-communicated lifespan, usually owing to some important customer need.  Contact jamshedd, rlander, and rbhanda for questions in this space. Some examples of operating systems we support beyond their normal end-of-life include Ubuntu  16.04 and Windows 7 / Server 2k8R2, but there will be more exceptions.
  - DncEng "matrix of truth": IlyaS, general SME : MattGal

### Process Inputs / Outputs

#### SME information:

Descriptions of what/where the inputs to the process come from (the answer to "what do I or the automated process neeed to consider to perform this task?", and what performing the below steps correctly achieves ("what comes out the other side?")

Inputs:
- The OS "Matrix of Truth" (future output from https://github.com/dotnet/core-eng/issues/11077).  Until this matrix exists, what we have is considered "in matrix" but we need to be removing unsupported OSes.
- The .NET Core release team's input (they receieve notifications for OS removals from build/test support via the partners DL, and can veto this with justification)
- EstimatedRemovalDate warnings / errors from the dotnet-helix-machines-ci pipeline

Outputs:
- Removal of Helix image definition from dotnet-helix-machines repository.
- Communication with the release PM team ensuring any "first time" removed 
- Communication blurb following the below template in weekly rollout email.  As these warnings are designed to appear 3 weeks prior to expiration, it should consistently allow the current week's rollout (or "not rolling out" mail) to indicate that users need to take action.

The following Helix Queues and/or Build images will be removed on the Wednesday rollout following the estimated date. Please remove usage of these queues/images before this date to keep your pipelines and tests functional.

Helix Queues:

| Queue Name | Estimated removal date |
| - | - |
| Some.Helix.Queue | 03/14/2022 |
| Some.Other.Helix.Queue | 03/10/2022 |

Pool provider images: (these will continue to work for some time after, but will eventually be cleaned up)

| Image Name | Estimated removal date |
| - | - |
| Build.OperatingSystem.KindOfImage | 03/21/2022 |
| Build.DifferentOperatingSystem.KindOfImage | 03/22/2022 |

Removing no-longer-supported operating systems on a regular cadence both allows us to be as secure as possible and use more of the resources we have available more for still-supported platforms.
If you feel this removal is in error, or need to extend support beyond this date, please email dnceng@microsoft.com with your concerns.

### Execution Steps

#### Fully-automatable routines:
- EstimatedRemovalDate notifications - Part of the dotnet-helix-machines-ci pipeline.
- Links:
  - Documentation: https://dnceng.visualstudio.com/internal/_git/dotnet-helix-machines?path=/definitions/readme.md
  - Pipeline: https://dnceng.visualstudio.com/internal/_build?definitionId=596
  - ImageFactory Wiki (includes access instructions): https://dev.azure.com/devdiv/XlabImageFactory/_wiki/wikis/XlabImageFactory.wiki (includes access instructions)
  - .NET PM team's OS Version Management calendar: https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/12624/OS-Version-Management-Calendar-2022 (Adjust year for the current year)

- Known issues impacting the area: None
- Known tech debt that may cause validation "blindness: 
  - We need to establish EstimatedRemovalDate for all Helix images / queues : https://github.com/dotnet/core-eng/issues/14994  (we can punt all we want by setting dates 100 years in the future). Until this is done, (ETA 11/30/2021; need to meet with release PMs) removing old stuff is a best-effort process.

#### Manual processes:

When the dotnet-helix-machines-ci build to be rolled out produces warnings with `{QueueOrImageName} has estimated removal date: {Date}, in the next three weeks. Please include this in communications about upcoming rollouts.`:

1. Review the date and confirm using internet search / .NET Release PM Team calendar and release notes.  Example: [.NET Core 6.0 release notes](https://github.com/dotnet/core/blob/main/release-notes/6.0/supported-os.md)
2. If the date is incorrect, consult with DncEng operations v-team to determine a new date.
3. If the date is valid, work with the .NET Engineering Services Rollout team ([Teams Channel](https://teams.microsoft.com/l/channel/19%3a72e283b51f9e4567ba24a35328562df4%40thread.skype/Rollout?groupId=147df318-61de-4f04-8f7b-ecd328c256bb&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)) to ensure that this is communicated with every partners "rollout" email until the removal has occurred.

When the dotnet-helix-machines-ci build to be rolled out fails with `{QueueOrImageName} has estimated removal date: {c.EstimatedRemovalDate}, which has elapsed.  Either extend this date in the yaml definition or remove it from usage to proceed.`:

1. Review the date and confirm using internet search / .NET Release PM Team calendar.  **Note the "Matrix of Truth" data, once it exists, supersedes any data in "Estimated Removal dates"
2. If the date is incorrect, consult with DncEng Operations v-team (or, use judgment) to determine a new date and extend it via pull-request to this repository
3. If the date is valid, remove the definition via pull request to this repository.  If we have erroneously never communicated this state, you may use discretion to set a date sometime in the future to keep the current status quo, but please include a comment over the definition explaining why.

- Known issues impacting the area: n/a
- Known tech debt: n/a
- Troubleshooting guide per-step, ideally tested by execution by an individual unfamiliar with the feature area(s) involved: None for now, can add as users hit issues.

#### Troubleshooting:

- Ensure all EstimatedRemovalDate values are expressed in MM/DD/YYYY format.  Accidentally using DD/MM/YYYY format will occasionally work (e.g. 11/10/2021 vs 10/11/2021)

### Validation Steps

After completing manual steps: (cadence TBD, but probably weekly before rollouts), perform the following checks and make a note in https://github.com/dotnet/core-eng/wiki/Helix-Machine-Management-Operations-Notes

Template (insert at top of wiki mentioned abov)
```
Date:
Executor of manual checks: (Github or MS alias)
Link to Production rollout pipeline: 

Pipeline state:
- Monitor the next dotnet-helix-machines pipeline execution.  No warnings or errors related to EstimatedRemovalDate time elapsing should be seen.  Provide a link to this pipeline execution.
- Monitor First Responders Teams channel for surprised users; in the case of erroneous deprecation, work with DncEng team for a hot fix.  In the case of expected removal, use discretion and help unblock the user.

Notes: 
- Anything interesting or unusual that happened as part of this week's check-in.
- Issue(s) falling out of the process for this week:
```





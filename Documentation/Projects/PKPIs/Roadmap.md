# PKPI Overview / Roadmap

[PKPI Epic](https://github.com/dotnet/arcade/issues/2778)

- [Scope](#scope)

- [Workstreams](#workstreams)

- [Phase 1 (official build health) roadmap](#phase-1---build-health)

- [Phase 2 (build state) roadmap](#phase-2---build-state)

- [Working groups](#working-groups)

The PKPI roadmap involves accomplishing key deliverables in two separate phases.

In phase 1, we will provide PKPI data about official build health.  Official build health includes indicators about official build health (timing / reliability), and dependency update health (are dependencies flowing? how often? etc...).

In phase 2, we will provide PKPI data about official build state.  Official build state includes indicators about dependency staleness, dependency flow, and subscriptions.

[Phase 1](#phase-1---build-health) completion date: 10/11

[Phase 2](#phase-2---build-state) completion date: [TBD]

## Scope

- Provide visualizations of PKPIs for all B.A.R. Channels with the specific focus on the ".NET Core 5 Dev Channel"

- Provide per repo insight into official build health.  Overall product health metrics are not specifically a goal of this epic but may become available metrics as a result of achieving goals that are in scope.

- PKPIs are based on data available from the "dnceng" account.  Some repos are still producing official builds and flowing dependencies from "devdiv".  Acquiring data from accounts other than "dnceng" is outside the scope of this work.

## Workstreams

The PKPI epic involves five different workstreams.  A "workstream" identifies a grouping for an area of work which contributes the PKPIs.

See the [ProductKPIs](#../../ProductKPIs.md) document for specific information about the PKPI workstreams.

## Phase 1 - Build health

This phase deals with understanding historical data of official builds to identify trends in build reliability, timing, and dependency update trends.

**`Build health` consists of the `official build` and `dependency updates` workstreams.**

At the end of phase 1, you should be able to answer questions like, "how long is my repo taking to build?", "what is the reliability of my official build?", "how often are dependencies seamlessly updating in my repo?", "how often are dependency updates failing in my repo?", etc...

### Phase 1 key dates

| Date | Deliverable | Notes |
| ---- | ----------- | ----- |
| 9/13 | PKPI Roadmap available |
| 9/18 | Checkpoint* 1 for build health trends | By this point we should review early visualizations of official build health and we should be able to identify missing data sources or data sources that need adjusting for "dependency update" visualizations. |
| 9/25 | Checkpoint* 2 for build health trends | Visualizations for dependency updates should be ready for review |
| 10/2 | Checkpoint* 3 for build health trends (if needed) | Final feedback |
| 10/9 | LTS review** ||
| 10/11 | Complete | Available for general use |

\* Checkpoint - meeting between WG and key stakeholders (Matt Mitchell / Jared Parsons) to review progress, acquire feedback, adjust course

\*\* LTS review - demonstrate phase 1 deliverables to LTS team (Mark Wilkie, Chris Bohm, Shawn Rothlisberger, etc...)

Note: After 10/9, we may be using staging data for visualizations depending on Arcade rollout schedules.

## Phase 2 - Build state

This phase deals with understanding the current state of a repo so that we can identify potential health concerns.  These indicators are genereally interesting to repo owners, but particularly interesting during a release cycle.

**`Build state` consists of the `dependency staleness`, `dependency flow`, and `subscriptions` workstreams.**

### Phase 2 key dates

Phase 2 key dates are not yet defined.  The working group needs to gather more education about the technologies involved (darc / barviz) for phase 2 before defining dates.  Investigation of those areas will occur in parallel with phase 1.  More information about phase 2 planning / dates will be provided near the completion of phase 1.  We will also take that time to reassess if there are key pieces of "build health" that were not provided during phase 1 and need to be addressed.

## Working Groups

### Phase 1 working groups

- Official build workstream

  - Primary: Epsitha Ananth (*note: on FR rotation 9/23 - 10/7)
  
  - Secondary: Chris Costa (providing additional support where needed) (*note: on FR rotation 10/7 - 10/21)

- Dependency update workstream

  - Primary: Michelle McDaniels, Megan Quinn

  - Secondary: Chris Costa (providing additional support where needed) (*note: on FR rotation 10/7 - 10/21)

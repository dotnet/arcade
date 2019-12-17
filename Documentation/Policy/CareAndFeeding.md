# Care and Feeding of Arcade

## Arcade Point Persons
The current owner for dotnet/arcade is Mark Wilkie <mawilkie@microsoft.com>.  The current point persons are:
- Alex Perovich <Alex.Perovich@microsoft.com>
- Jon Fortescue <Jonathan.Fortescue@microsoft.com>
- Michael Stuckey <Michael.Stuckey@microsoft.com>
- Ricardo Arenas <riarenas@microsoft.com>

The main responsibilities of the point people are:
- Answer questions, provide guidance, and generally be helpful
- Pay attention.  Monitor the activity in the repo and take the appropriate action when necessary
- Triage, including old PRs (see section below for more on this)

## Triage / Managing the Backlog
- Schedule a general "scrub" at least once a year
- Use the template to determine if FR ([First Responders](https://github.com/dotnet/core-eng/wiki/%5Bint%5D-First-Responders)) should be involved

### General guidelines for triage (not rules, just guidelines)
- Issues older than 90 days, aren’t assigned to anyone, is not tracking something, and don’t belong to an epic are candidates to be closed
- Issues with only a title, or one liners should be considered for closure

Looked into if it made sense to use the same [triage guidance](https://github.com/dotnet/consolidation/blob/master/Documentation/issues-pr-management.md) as the consolidated repo, but the overlap appears minimal.  However - this should still be considered a possiblity to provide consistency.

## Filing Issues
All issues regarding Arcade (and related like darc, Maestro, Helix, etc) must be filed in the dotnet/arcade repo.  As an aside, the dotnet/core-eng repo is used by the engineering services team.

The default Arcade issue template should be used with every issue filed.  This implies that the template should be *very* simple and have low overhead.

At this time, the only items in the template are:
- Is this issue blocking (yes/no)
- Is this issue causing unreasonable pain (yes/no)

## Working Group Syncs
The wider Arcade working group will meet every two weeks, or on demand.  In addition, arcadewg@microsoft can be used for infrastructure related items.

## Alerting
At this time, it is one of the roles of the point persons to ensure that "alerting" happens.  However, it would be advantageous to provide some automation so help highlight issues/PRs in Arcade that need more immediate attention.

For example perhaps alert on items such as:
-   Template marked as blocking or unreasonable pain
-	More than n comments in x timeframe
-	Sentiment (angry, upset, distressed)
-	@somebody on our team

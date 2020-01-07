# Contributor Guidance for Arcade
Over and above the [.NET Core contributor guidelines](https://github.com/dotnet/coreclr/blob/master/Documentation/project-docs/contributing.md) (which are important), there are some principles and guidelines that are specific to Arcade as well.

For the most part, contributions to Arcade are straight forward and relatively smooth.  However, from time to time, getting changes in can be challenging, and even frustrating.  The very nature of Arcade is that it's shared across multiple teams.  This document attempts to clarify some of the expectations as well as provide some 'advice' for success when contributing to Arcade.

## Contribution Principles
* Should benefit multiple repos across .NET Core.
* Every contributor is "heard" and taken seriously every time
* Keep it simple. (don't over engineer or be "clever")
* Focus on value over conviction.  (of course conviction should help drive the value discussion)
* Opinions and beliefs must be substantiated with data and a strong business rationale.
* All discussions (arguments) must be civil, respectful, productive, and above all - kind. The focus must always be on the topic, not individuals.
* Work in 'partnership' (shoulder to shoulder) as opposed to 'us' vs. 'them'.

## Arcade Core Team
### Expectations
- Answer questions, provide guidance, and generally be helpful and responsive.
- Pay attention.  Monitor the activity in the repo and take the appropriate action when necessary
- Triage, including old PRs (see section below for more on this) 

### Ownership
The current owner for dotnet/arcade is Mark Wilkie <mawilkie@microsoft.com>.  The current point persons are:
- Alex Perovich <Alex.Perovich@microsoft.com>
- Jon Fortescue <Jonathan.Fortescue@microsoft.com>
- Michael Stuckey <Michael.Stuckey@microsoft.com>
- Ricardo Arenas <riarenas@microsoft.com>

## Getting Things Fixed
### Submit a PR
This is probably the most straight forward way.  If the PR might take some time and you want to get an early read first, filing an issue to get a discussion going, or emailing arcadewg@microsoft.com should get the insight you need.

### Filing Issues in Arcade
All issues regarding our shared infrastructure (including Arcade, darc, Maestro/dependency flow, and Helix) should be filed in the dotnet/arcade repo.  If there's a doubt, file in Arcade.  (As an aside, the private dotnet/core-eng repo is used by the engineering services team for internal facing work.)

The default Arcade issue template should be used with every issue filed.  The template is *very* simple and has low overhead.

### Triage and Backlog Management
- New Arcade issues are triaged at least once a week.  
- Issues which have been marked via the template as blocking or causing unreasonable pain are looked at right away via FR ([First Responders](https://github.com/dotnet/core-eng/wiki/%5Bint%5D-First-Responders))
- General "scrubs" happen at least once a yea.r


### General guidelines used for triage (not rules, just guidelines)
- Issues older than 90 days, aren’t assigned to anyone, is not tracking something, and don’t belong to an epic are candidates to be closed
- Issues with only a title, or one liners will be considered for closure
- Pull requests without a description / linked issue make it hard to understand priority or reasoning behind a change. Is this fixing an annoyance? a blocking bug?
- Clean ownership helps keep things moving.

### Working Group Syncs
The wider Arcade working group (arcadewg) will meet every two weeks, or on demand.  In addition, arcadewg@microsoft can be used for general infrastructure queries.

## Conflict Resolution / Arbitration / Escalation

### What if I feel stuck and want to escalate?
What happens when there's just no discernable path forward?  Who is the final arbitrator?  What to do in those rare occasions where everyone is stuck...

The short answer is that the final arbitrator is the Arcade owner, which currently is @markwilkie.  However...before jumping in and asking for a 'judgement', there are a few things that need to happen first:

* Let **time** go by, while continuing to poke, discuss, re-think and so on.  Most things don't need resolution urgently (even though we often feel urgent about them) and usually the right path forward gets more clear over time.  In cases where it is urgent, finding a short term 'hack' is perfectly acceptable.
* Keep channels of communication open, and stay in there with tenacity.  In disagreements which are tough, if the dev pushing for a change loses heart, then likely nothing will happen.  Stay at it and keep up the pressure.  This is often tough to do...
* Have at least three (3) face-to-face conversations (or voice when remote) over a week or two at a minimum with the devs where there is the most disagreement.  It is amazing how much these higher bandwidth conversations help.  (Community contributors get a pass here...)
* Be able to articulate all other points of view.  Know the rationale behind the differences.

Time and again differences get worked out when these items occur.  To be sure, it's not easy and we're often tempted to think "it shouldn't be this hard".  While perhaps that's true - the reality is that sometimes it IS this hard, and there's really no easy path through.  The good news is that in Arcade, this is relatively rare.

## Helpful Tactics/Advice if it gets challenging
* Where there are differences that are challenging, see the '[Defaults Guidance](./DefaultsGuidance.md)' doc for help on framing and definitions.
* Always feel free to reach out to the Arcade owner (currently @markwilkie) for advice and next steps and another perspective.
* Search for common ground, then work out from there.
* Remember that there's no such thing as "winning" an argument.  There's only working to find the best path forward for .NET Core.
* Be flexible and willing to think about something completely differently.  After all, that's what learning is all about.
* Refrain (where possible) from relying solely on GH comments to find resolution to a particular tricky disagreement.  Face-to-face when possible, or at the very least, voice conversations are much higher bandwidth and thus, dramatically more effective.
* Initially, leave your own personal 'principles' at the door.  Instead work with others to find the right pragmatic next step, but then circle back around and use your principles to help you push for the right longer term solution/approach over time.
* Take the opportunity to learn from others.  We're all learning together, and the skills required to reconcile differences are very valuable in every venue.

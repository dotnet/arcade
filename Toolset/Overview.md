#ToolShed / Arcade (code name being discussed...)

##Sharing Infrastructure Across .NET Core

####Overview / Introduction
We need well-understood and consistent mechanisms to consume, update, and share engineering infrastructure across the .NET Core team.  

The primary concept is to break the infrastructure into “pay for play” components, such that one piece of functionality can be consumed, with minimal dependencies.  The idea is to not force unnecessary dependencies, thus making it more reasonable to consume only what is needed.

This approach publishes what amounts to “public surface area” for the shared engineering infrastructure.  These “contracts” then allow for the product teams to reason about how (or if) they participate and manage their engagement with the common infra over time.  In short, the product teams “pull” what is needed, when it’s needed.

This document speaks _only_ to the first bullet point below - e.g. tasks/targets as Nuget packages.  The rest of the bullets are for context only.

####Methods for Consuming the .Net Core Shared Infrastructure Components

-	**MSBuild tasks/targets as Nuget packages**  (<-- what this doc is about)
-	VSTS extensions  (check the box in VSTS)
-	Hosted services with REST end points which are owned by the Engineering Services
-	Toolsets (think compilers, training tools, etc) as binaries in their own setup or Nuget packages
-	Machine (VM) images and/or Docker containers
-	"Resources" as planned by VSTS.

####Approach
-  The idea is to start with the "satellite" (non core) build tools so we can learn what works and what doesn't.  Once we're more comfortable, we can start to move farther into the "core" (touchier) tools.
-  The assumption is that the ProdCon V2 effort will be largely addressing the repo level contracts themselves.  As mentioned earlier, the intent of this effort is to focus on the tools.  Note that there is likely some overlap (like with bootstrapping), but we'll deal with those as they come up.
-  We like to take advantage of the ProdCon V2 effort to start off well.  To that end, there is some urgency so that we can be ready.

####Business Value (to remind us why we think we should do this)
-  Build on the success of others.  Namely, being able to _reasonably_ share functionality across teams and repos. 
-  Control and ownership.  Repo owners/devs can manage _what_ tools are needed (and which aren't), as well as _when_ they take it.  This includes not only new functionality, but almost more importantly, updates to existing.
-  Dev work flow.  Allows devs to "plug and play" when modifying or bringing up new functionality in the build without having to re-invent the wheel.  In addition, the _how_ of build tools is largely understood - even across repos.
 

####Toolset Nuget Package Requirements
-	If used by the build, the tool should be packaged, deployed, and consumed as a Nuget package.  (compilers, training tools, etc are out of scope.  See above list)
-	Every package must be versioned.  (separate one-pager to flesh this out further)
-  There needs to be a clear and easy way to bootstrap (get started and/or add a package).
-	Each package is serviceable (forkable) itself, and can be easily used for servicing of the product.  The idea is that the common infra should “fork” with the product repo branch.  (see below for implementation notes/questions as this area is likely challenging to get right)
-	Each contract represents a single area of work.  In other words, there is specific “intent” for each contract/package.  (contrasted with general “helper” stuff, or lumping several things together)
-	New packages should be reviewed by the the product teams in conjunction with Engineering Services.  (think API reviews…)
-	Existing consumption of common infra will be migrated to this new approach, piecemeal over time as appropriate.  No giant “switch”….  (plus, some things don’t need to migrate)
-	In addition to unit tests at the code level, end-to-end validation tests are required to ensure the contracts are still fulfilled.
-   Each package should carry its own documentation that is updated when the tool/package is updated.
-	Each contract includes telemetry publishing for both a) usage and errors, and b) visibility (like Mission Control)

####Out of scope for this specific project
-  Repo level contracts.  We do need to unify (at a high level) the "verbs" we use to interact with the repo.  (e.g. build, test, etc)  However, for this specific exercise, that work is out of scope.  Please note that much of this will likely be done as part of the ProdCon effort.
-  Tool chains like compilers, training, and the like.  The current thinking is to fully implement "config as source" where the VM/Container contains the right tools what what is needed.

####Random Implementation Notes/Questions
-  Tracking epic: https://github.com/dotnet/core-eng/issues/2548
-  Bootstrapping must be simple and allows for easy version selection  (current thoughts: https://github.com/chcosta/roslyn-tools/blob/bootstrap/docs/Toolset-Bootstrap.md)
-  There is one (separate from product) feed for all common infrastructure (this is probably correct, but needs to be thought through. (https://github.com/dotnet/core-eng/pull/2552)   For example, what about our community?  Also, private repos?)
-  A method of discovering what shared infra offerings are available is important to figure out.  Likely this is largely a documentation effort, but perhaps there can be a special service which helps out.  Possibly use the "gallery" feature as an aid?  Documentation for each package will help too.
-  Working out the right way to “fork” infra (in this case packages) with the product branch needs additional attention.  The current thinking is to embed the git hash into every binary/package produced, and then ensure the repo is buildable clean given a specific hash.

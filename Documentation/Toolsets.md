# Toolsets

## Overview
Various tools exist that are not built in Arcade, but are still needed to support both bootstrapping (both managed and native), as well as various build related workloads needed by different repos.  This document attempts to address the requirements for acquiring these tools that will work for Arcade, and thus the rest of the repos in the .NET Core stack.

## Requirements
* Enable the toolset acquisition portion of 'clone' + 'build' (no prereqs)
* Each tool must have a clear owner (see 'ownership' section below)
* Works for all supported OS's 
* Ability to specify (or override default) which version of a tool is needed
* Updates are only made after there is reasonable confidence they won't break 
* Both managed and native tools (including any dependencies) must be supported
* It should be possible to reasonably get required tools for new platform bring up.  (distros not yet supported by core)

## Ownership
Each tool/package not built by Arcade, but needed by .Net Core must have a clear owner.  This ownership must encompass the following:
* Current documentation sufficient to onboard and use the tool for the main supported scenarios
* It must be clearly documented how to get needed updates or fixes done.
* All documentation must be included with the tool
* No breaking changes

## Basic Mechanics
At a high level, toolsets not built by Arcade are managed by:
* Known location in Azure blob storage for all tools not built in Arcade
* Scripts acquire the correct tools as part of the bootstrapping process
* Scripts kept up to date by updating every participating repo (Maestro++)
* Community uses the same Azure blob storage end points MSFT does to get the tools from

## Servicing
* See the ["Servicing" document](Documentation/Servicing.md) for a broader discussion of servicing and its policies
* New tools and tool versions are added to the Azure blob location.  (no replacements, only adds)
* Script updates are deployed using automation to every repo
* When absolutely necessary, a repo can "pin" to a specific "channel", or branch/fork.  (see "servicing" document for policy around this)

## Open questions/investigations
* Understand the specifics for how to host .exe's (instead of nuget packages) in the feed.
* What x-plat provisions should be made (if any) for the scripts?  (e.g. ps)
* See if VisualStudio is indeed an exception for prereqs on Windows
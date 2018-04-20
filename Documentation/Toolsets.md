# Toolsets

## Overview
Various tools are needed to support both bootstrapping (both managed and native), as well as various build related workloads needed by different repos.  This document attempts to address how this will work for Arcade, and thus the rest of the repos in the .NET Core stack.

## Requirements
* Enable the toolset acquisition portion of 'clone' + 'build' (no prereqs)
* Works for all supported OS's 
* Ability to specify which version of a tool is needed (override default)
* Scripts which acquire the tools are automatically deployed to all participating repos  (Maestro++)
* Updates are only made after there is reasonable confidence they won't break 
* Both managed and native tools (including any dependencies)
* It should be possible to reasonbly get required tools for new platform bring up.  (distros not yet supported by core)

## Basic Mechanics
At a high level, toolsets are managed by:
* Known location in Azure blob storage for all tools
* Scripts acquire the correct tools as part of the bootstrapping process
* Scripts kept up to date by updating every participating repo (Maestro++)
discussion as of 4/18)

## Servicing
* See the ["Servicing" document](Documentation/Servicing.md) for a broader discussion of servicing and its policies
* New tools and tool versions are added to the Azure blob location.  (no replacements, only adds)
* Script updates are deployed using automation to every repo
* When absolutely necessary, a repo can "pin" to a specific "channel", or branch/fork.  (see "servicing" document for policy around this)

## Open questions/investigations
* Understand the specifics for how to host .exe's (instead of nuget packages) in the feed.
* What x-plat provisions should be made (if any) for the scripts?  (e.g. ps)
* See if VisualStudio is indeed an exception for prereqs on Windows
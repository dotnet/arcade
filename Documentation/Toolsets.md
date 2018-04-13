# Toolsets

## Overview
Various tools are needed to support both boostrapping (both managed and native), as well as various workloads needed by different repos.  This document attempts to address how this will work for Arcade, and thus the rest of the repos in the .NET Core stack.

## Requirements
* Enable the toolset aquisition portion of 'clone' + 'build' (no prereqs)
* Works for all supported OS's 
* Ability to specific which version of a tool is needed (override default)
* Scripts which aquire the tools are automatically deployed to all participating repos  (Meastro++)
* Both managed and native tools

## Basic Mechanics
At a high level, toolsets are managed by:
* Known location in Azure blob storage for all tools
* Scripts aquire the correct tools as part of the bootstrapping process
* Scripts kept up to date by updating every participating repo (Maestro++)
* Community needs to install the tools as prereqs - there's no provision to automatically bring them down

## Servicing
* See the "Servicing" document for a broader discussion of servicing and its policies
* New tools and tool versions are added to the Azure blob location.  (no replacements, only adds)
* Script updates are deployed using automation to every repo
* When absolutely necessary, a repo can "pin" to a specific version.  (see "servicing" document for policy around this)

## Open questions/investigations
* Understand the specifics for how to host .exe's (instead of nuget packages) in the feed.
* What x-plat provisions should be made (if any) for the scripts?  (e.g. ps)
* See if VisualStudio is indeed an exception for prereqs on Windows
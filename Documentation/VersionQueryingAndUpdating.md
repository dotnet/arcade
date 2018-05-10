# Querying and updating repo dependencies

## Goal

The main goal of this set of tools is to centralize the way we maintain the bootstrapping scripts in sync across participating repos and at the same time have a single way of updating the dependencies on each repo, keeping track of what’s in each product at all times. 

## Description

The process of reading and updating scripts in the repos will change depending on the environment where the operation is being executed, i.e. Maestro++ for remote, Darc for local. Darc and Maestro++ are responsible for obtaining repo inputs and handling outputs. The mechanics of querying, reading and updating versions will be contained in the DarcLib class library.

![Diagram](VersionQueryingAndUpdating.png)

## Scenarios

Here I list the top level scenarios which come to mind and try to describe the required steps to accomplish them:

#### How to update arcade scripts across all repos without using Maestro++

Dev generates a change in arcade by adding, updating or removing a script. Depending on the operation there is a version which needs to be updated in participating repos.

For each participant repo:

1.	Dev uses Darc to update a dependency in current repo (the update itself is done through DarcLib)
2.	Dev uses Darc to commit and push the update
3.	Auto-PR is approved and merged

#### How to update a package using Maestro++

1.	Maestro++ is triggered by actions such as package publishing and/or GitHub webhooks monitoring changes in files like bootstrapping scripts or even a manually
2.	Maestro++ uses Darc to determine which need to be updated
3.	For each repo which needs the update Maestro++:
a.	Uses Darc to update a dependency in current repo (the update itself is done through DarcLib)
b.	Uses Darc to commit and push the update
c.	Auto-PR is approved and merged

#### How to find out who’s using package n, and what versions are out there

1.	Dev/Maestro++ uses Darc to query for package n
2.	Dev/Maestro++ uses Darc to query for package n’s versions

#### How to determine the versions of the packages contained in a final build

1.	Dev/Maestro++ uses Darc to query for build version v
2.	Darc returns the dependency graph under build v

## Components

### DarcLib

Class library used by Darc, Maestro++, etc. to perform version operation on participating .Net Core repos. Its main focus is the mechanics of reading and writing version information in the local repo which it is being executed in but also it is able to query and update the global version reporting store.

DarcLib will be able to:

*  Query dependencies in description
*  Query sha+repositories for dependencies in description
*  Update a description to move a dependency to a new version
*  Query the reporting system for shas+repositories in which a versioned item was used
*  Query the reporting system for shas+repository that produced a versioned item
*  Query locations of official assets for a versioned item

You can find more information about dependency descriptions [here](DependencyDescriptionFormat.md)

### Darc

A command line tool which consumes DarcLib and which purpose is to query and update version information from repositories. Its functionality is:

#### Input: local repository 
*  Query versioned items in repo dependency description (using DarcLib)
*  Query shas+repositories for dependencies in the repository dependency description (using DarcLib)
*  Query shas+repositories for all downstream dependencies (using DarcLib)
*  Alter the package+version+sha+repository information in the dependency description (using DarcLib)
    *  Add new dependency 
    *  Change existing dependency 
    *  Remove dependency

#### Input: sha+repo              
*  Query reporting system for versioned items produced by sha+repository (using DarcLib) 
*  Query reporting system for versioned items in which this sha+repository is referenced (using DarcLib) 
*  Query reporting system for shas+repositories in which this sha+repository is referenced (using DarcLib) 

#### Input: package+version
*  Query binary (if known format) for sha+repository (using DarcLib)

#### Common functionality
*  Create source layout for matching versions in repo dependency tree.
*  Download product+tools of official build for matching versions, sha+repository, package+version

### Maestro++
*  Identify when there is an update in Arcade’s scripts/dependencies and
    *  Add new dependency (using DarcLib for versions)
    *  Change existing dependency (using DarcLib for versions)
    *  Remove dependency (using DarcLib for versions)
for each participating repo depending on each repo’s subscriptions

### Darc Vs. Maestro++

The main difference between these two is that Darc should be used in single local and Maestro++ when a change has to be applied to all participating repos. Maestro++’s role could also be done by Darc but since it is a manual work the dev would need to do the same in N repos where N represents the number of participating repos. 

Also, Maestro++ could use Darc to deal with all the updates in which case the only notion Maestro++ has about the process is which repos to update and the subscriptions of each.


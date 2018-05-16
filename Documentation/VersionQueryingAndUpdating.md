# Querying and updating repo dependencies 

## Goal 

The main goal of this set of tools is to centralize the way we maintain 
the bootstrapping scripts in sync across participating repos and at the 
same time have a single way of updating the dependencies on each repo, 
keeping track of what’s in each product at all times. 

## Description 

The process of reading and updating scripts in the repos will change 
depending on the environment where the operation is being executed, i.e. 
Maestro++ for remote, Darc for local. Darc and Maestro++ are responsible 
for obtaining repo inputs and handling outputs. The mechanics of 
querying, reading and updating versions will be contained in Darc. 

![Diagram](VersionQueryingAndUpdating.png) 

## Scenarios 

Here I list the top level scenarios which come to mind and try to 
describe the required steps to accomplish them: 

#### How to update arcade scripts across all repos without using 
Maestro++ 

Dev generates a change in arcade by adding, updating or removing a 
script. Depending on the operation there is a version which needs to be 
updated in participating repos. 

For each participant repo: 

1. Dev uses Darc to update a dependency in current repo
2. Dev uses Darc to commit and push the update 
3. Dev uses Darc to create the Auto-PR 
4. Auto-PR is approved and merged by dev 

#### How to update a package using Maestro++ 
	
1. Maestro++ is triggered by actions such as package publishing and/or 
GitHub webhooks monitoring changes in files like bootstrapping scripts 
or even a manually 
2. Maestro++ uses Darc to determine which need to be updated 
3. For each repo which needs the update Maestro++: 
    1. Uses Darc to update a dependency in current repo
    2. Uses Darc to commit and push the update 
    3. Auto-PR is approved and merged. Initially even though a PR is
    created by Maestro++ it will require a human to approve

#### How to find out who’s using package n, and what versions are out there 

1. Dev/Maestro++ uses Darc to query for package n 
2. Dev/Maestro++ uses Darc to query for package n’s versions 

#### How to determine the versions of the packages contained in a final build 

1. Dev/Maestro++ uses Darc to query for build version v 
2. Darc returns the dependency graph under build v 

## Components 

### Darc 

A command line tool which perform version operations on participating .Net Core repos. 
It contains the mechanics of reading and writing version information in a repo as well as to query 
and update the global version reporting store.  Its functionality is: 

#### Input: dependency data 

* Query dependencies in `Version.Details.xml` 
* Query sha+repositories for dependencies in `Version.Details.xml` 
* Update an entry in `Version.Details.xml` to move a dependency to a new version 
* Query the reporting system for shas+repositories in which a versioned item was 
used 
* Query the reporting system for shas+repository that produced a versioned item 
* Query locations of official assets for a versioned item 

You can find more information about dependency descriptions [here](DependencyDescriptionFormat.md) 

#### Input: local repository 
* Query versioned items in repo's `Version.Details.xml`
* Query shas+repositories for dependencies in the repo's `Version.Details.xml`
* Query shas+repositories for all downstream dependencies
* Alter the package+version+sha+repository information in `Version.Details.xml`
    * Add new dependency 
    * Change existing dependency 
    * Remove dependency 

#### Input: sha+repo 
* Query reporting system for versioned items produced by sha+repository
* Query reporting system for versioned items in which this sha+repository is referenced 
* Query reporting system for shas+repositories in which this sha+repository is referenced 

#### Input: package+version 
* Query binary (if known format) for sha+repository

#### Common functionality 
* Create source layout for matching versions in repo dependency tree. 
* Download product+tools of official build for matching versions, sha+repository, 
package+version 

### Maestro++ 
* Identify when there is an update in Arcade’s scripts/dependencies and 
    * Add new dependency (using Darc for versions) 
    * Change existing dependency (using Darc for versions) 
    * Remove dependency (using Darc for versions) 
for each participating repo depending on each repo’s subscriptions 

### Darc Vs. Maestro++ 

Mestro++ based on times or triggers (i.e. a change in a file) flows the new dependency
into subscribed repos using Darc. Darc makes the required updates, commits the changes
and starts a new PR.
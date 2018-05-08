# VSTS Guidance

## Projects
There are two projects for use.  They are:
-  DotNet-Public
    -  Used for CI
    -  For build definitions only  (no source code - that's on github)
    -  Build definitions are allowed to pull source directly from GitHub
-  DotNet-Internal
    -  Build definitions are only allowed to pull source from internal repos
 
TODO: figure out the deal with default repo

## Teams

In this context, teams pretty much only affect which level the kanban (or whatever) boards are at.  While permissions can be set at the team level, we're choosing not to do so.

Teams:
-  $(GitHubOrg)

## Permissions
To keep things as simple (manageable) as possible, we're going to manage permissions coarsely at the project level - pointing directly to existing AD security groups managed in idweb.  **We should not be managing permission outside of this method**

-  Permissions will point to existing security groups in AD which are managed in idweb.  This admin is done at the **project** level.  ([vsts link](https://dotnet.visualstudio.com/DotNet-Internal/_admin/_securit))
-  The bulk of folks will be in the 'contributers' group, with special additions for other groups (like admin)
-  There are VSTS permission groups that can be set out side of the project. ([vsts link](https://dotnet.visualstudio.com/_admin/_security))   **We're not going to use those**
-  It is also possible to set permissions at the team.  **We're not going to do that**

## Build Definitions

Folder names for github repos: 
- $(GitHubOrg)/$(GitHubRepoName)/*.def
 
Folders for VSTS repos:
- Put it where it makes sense, just not top-level
- Use the closest github org
- After that, $(VstsRepoName)/*.def
 
Build definition file name convention:
- kebab-case, No spaces, use dashes
- Pattern: $(RepoName) - $Scenario - $Suffix
  - Scenario: (optional)
    - code-coverage
    - slow-tests
    - fast-tests
    - internal-tools (TBD -- Nate and Matt to investigate more)
    - official
  - Suffix:
    - internal = dotnet-internal builds
    - public = dotnet-public builds
 
TODO: figure out how to control definition names automatically created by VSTS

## Source Code

For now, everything should be in Dotnet-Internal and any code that is public should be on GitHub
 
VSTS repos should:
-  Be mirrored from GitHub, if not internal only.
-  Internal-only projects should only be in the DotNet-Internal project with no github equivalent
 
Naming conventions
-  $(OrgName)-$(RepoName)-{Internal|Public*}   (we're not using 'trusted' anymore.  It's redundant)
-  Again - *No plan to have public repos in VSTS at this time

## Terms

From time to time, there are some terms you might encounter in documentation or otherwise.  Here's some I've run across so far and the interpretation.
-  collection  --> account --> instance  (top level thing - e.g. devdiv.visualstudio.com)
-  team --> group of indivduals.  Largely is about the backlog, not much more.  In our case we're not using for permissions.
-  

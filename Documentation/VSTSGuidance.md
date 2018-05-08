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

Being figured out...

## Permissions

Being figured out...

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


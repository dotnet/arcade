# CodeQL Guidance

## Background

CodeQL is a code analysis platform owned by Semmle, now a subsidary of GitHub. It provides value by using extractors to construct a database representing the codebase, then providing a query language to perform sematic analysis. CodeQL instruments the build of compiled languages, and directly analyzes source code for interpreted languages. 

CodeQL is required as part of Microsoft's Security Developent Lifecycle (SDL) requirements. .NET Engineering Services supports CodeQL via the Guardian toolset with scan results published to Trust Services Automation (TSA). 

CodeQL adds a significant time to builds. We therefore recommend creating a new, seperate pipeline instead of incorporating CodeQL scans into existing PR or testing pipelines. 

## TL;DR: Quickstart

Arcade provides new templates to encapsulate the build process and start the Guardian-driven CodeQL engine. They may be used from an "empty" pipeline definition 

1. Ensure your repository has the latest Arcade version
2. Copy Arcade's CodeQL pipeline definition file [`azure-pipelines-codeql.yml`](https://github.dev/dotnet/arcade/blob/main/azure-pipelines-codeql.yml) to your repository
3. Modify the pipeline definition to work for your repository's needs. 
   - For projects using compiled languages, like C#, update the `buildCommand` to build the project. Unlike other Guardian tools, the CodeQL engine executes these in an instrumented environment to enable analysis. Note that if not provided, the engine may use heuristics to build. 
   - Update the `language` parameter to match your project's language. Valid values are `cpp`, `csharp`, `javascript`, `java`, `python`, and `go`
   - For projects with multiple languages, duplicate the Job for each. 
4. Create a new Pipeline executing this newly-created definition. 


## Use with Arcade

Arcade version (arcade version) provides a new Job template "[codeql-build.yml](https://github.com/dotnet/arcade/blob/main/eng/common/templates/jobs/codeql-build.yml)" and step template "[execute-codeql.yml](https://github.com/dotnet/arcade/blob/main/eng/common/templates/steps/execute-codeql.yml)". It extends Arcade's existing use of Guardian to provide SDL tooling and workflow.

A working example pipeline defintion follows.

```yaml
#Include variables for SDL/TSA publishing
variables:
  - name: _TeamName
    value: DotNetCore
  - group: SDL_Settings
    
# Execute only on a schedule, once each week
trigger: none

schedules:
  - cron: 0 12 * * 1
    displayName: Weekly Monday CodeQL/Semmle run
    branches:
      include:
      - main
    always: true

# One stage, one job: just CodeQL
stages:
- stage: build
  displayName: Build
  jobs:
  - template: /eng/common/templates/jobs/codeql-build.yml
    parameters:
      jobs:
      - job: Windows_NT_CSharp
        timeoutInMinutes: 90
        pool:
          name: NetCore1ESPool-Internal
          demands: ImageOverride -equals windows.vs2019.amd64

        steps:
        - checkout: self
          clean: true

        - template: /eng/common/templates/steps/execute-codeql.yml
          parameters:
            executeAllSdlToolsScript: 'eng/common/sdl/execute-all-sdl-tools.ps1'
            buildCommands: 'build.cmd -configuration Release -ci -prepareMachine'
            language: csharp
            additionalParameters: '-SourceToolsList @("semmle")
            -TsaInstanceURL $(_TsaInstanceURL)
            -TsaProjectName $(_TsaProjectName)
            -TsaNotificationEmail $(_TsaNotificationEmail)
            -TsaCodebaseAdmin $(_TsaCodebaseAdmin)
            -TsaBugAreaPath $(_TsaBugAreaPath)
            -TsaIterationPath $(_TsaIterationPath)
            -TsaRepositoryName "dotnet-release"
            -TsaCodebaseName "dotnet-release"
            -TsaPublish $True'
```

Much of this template can be used as-is for most repositories. The important elements for customizatino are the input parameters to the `execute-codeql` template. 

- `buildCommands`: For compiled languages, the command to build the project. For interpreted languages, this may be excluded. 
- `language`: The language target for analysis (as known by CodeQL)
- `additionalParameters`: These are the typical TSA configuration options usually already populated for .NET projects. Ensure that `TsaRepositoryName` and `TsaCodebaseName` are correct for your repository.

For more information on SDL/Guardian in Arcade, see [How To Add SDL Run To Pipeline](https://github.dev/dotnet/arcade/blob/main/Documentation/HowToAddSDLRunToPipeline.md).

## Alert suppression

Suppression may be done using inline comments (in whatever comment form is appropriate for the language). The comment must appear on the same line as the alert, or the first line if the alert spans multiple lines. 

A suppression comment is made of:

1. The string "lgtm[^1]" (case insensitive)
2. A query ID surrounded by square brackets
3. A justification string of at least 25 characters

For example, in C#,

```cs
// lgtm [cs/weak-crypto] Algorithm needed per standard and contained safely here
```

Language-specific examples and some variations may be found in LGTM's [Alert Suppression](https://lgtm.com/help/lgtm/alert-suppression) document.

[^1]: At the time of this writing, tools expect `lgtm` instead of `codeql`. This may change as versions evolve and the new name propogates.

## Further Reading

GitHub's official CodeQL documentation: [CodeQL documentation](https://codeql.github.com/docs)

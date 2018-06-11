# Writing VSTS Build Definitions

The purpose of this document is to detail how VSTS YAML build definitions should be written to integrate into the .NET Core Ecosystem and take advantage of the scaffolding availabe in the Arcade repository.

## General Goals For Build Definitions

Build definitions for the .NET Core Ecosystem should be written with the following goals in mind:

- **A build definition integrates with telemetry systems**

  - Builds should integrate with telemetry systems to enable a number of scenarios important to building a more effective .NET Core infrastructure ecosystem, including:
    - Enable build status tracking.
    - Enabe pass/failure analysis.
    - Enable test failure analysis.
    - Enable efficient product code flow via Maestro# and Speculative Package Flow.
    - Enable efficient asset tracking.

- **A build definition can be used for both OSS and internal development** - To reduce the cost of developing in both the open and closed worlds, sharing the same build definition logic is ideal.  A definition should be able to be run in both OSS and internal contexts and produce results accordingly (e.g. an internal run signs, an OSS run doesn't).  The definition should also be usable in both CI and Official builds (open or closed).

- **A build definition results are easy to reproduce locally** - Build definitions, especially when being viewed through the context of a recent build log, should be able to be reproduced locally (if possible).  Ambient state dependencies should be minimized where possible.

- **A build definition can be serviced and altered globally to maintain consistency across the .NET Core ecosystem** - The number of build definitions is large, and making extensive changes across the ecosystem must be efficient.  For example, changes or rollout of new telemetry should be relatively painless.

- **A build definition change must be testable in an isolated environment** - A build definition change should be testable without altering the state of other in-progress changes.

- **A build definition should branch with the code it serves and maintain its functionality over the servicing lifetime of the code** - Ensure that not only does the build definition not drift from the source it serves, but also does not become non-functional over time due to changes in external systems.

## General Rules

- **A build definition is defined in code (YAML)** - Defining as much build definition configuration in source ensures a testable, servicable, and versionable infrastructure ecosystem.

- **A build definition does not rely on input variables in the environment** - Environment variables cause lots of issues in build definitions and should be avoided as a means of passing input to a build where possible.
  - They may not be obvious to readers of logs.
  - Case-insensitivity differences between operating systems and tools is a cause of issues.
  - Environment block size limits can occasionally be a problem.
  - Reproducing results can be more difficult.

- **A build definition utilizes provided templates where possible** - Utilizing templates gives infrastructure developers extension points or abstraction layers to alter or extend behavior.  For example, templates can:
  - Ensure the right telemetry systems are contacted during key points in the build.
  - Allow usage of EOL operating systems to be removed or at least identified more easily.
  - Simultaneously roll out new changes to repo scripting and build definition logic.
  - Update older servicing branches to ensure their build definitions are not broken by changes in external systems.
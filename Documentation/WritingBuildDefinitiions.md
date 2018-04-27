# Writing VSTS Build Definitions
The purpose of this document is to detail how VSTS YAML build definitions should be written to integrate into the .NET Core Ecosystem and take advantage of the scaffolding availabe in the Aracde repository.

## General Goals For Build Definitions
A build definition for the .NET Core Ecosystem should be written with the following goals in mind:
- **The build definition is defined in code (YAML)**
- **The build definition integrates with telemetry systems**
- **The build definition utilizes provided templates where possible**
- **The build definition can be used for both OSS and internal development**

## General Rules
- 
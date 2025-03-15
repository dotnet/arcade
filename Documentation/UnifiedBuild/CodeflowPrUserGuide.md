# Codeflow PR User Guide

## What is Codeflow?

Codeflow PRs are automated pull requests created by the [Product Construction Service (PCS)](https://github.com/dotnet/arcade-services), that are used to manage the dependencies that exist among dotnet repositories, as well as synchronizing all of the source code from dotnet repositories into a [Virtual MonoRepo (VMR)](https://github.com/dotnet/dotnet). Previously, automated PRs were used to update dependencies among dotnet repositoires, but source code was not synchronized. The introduction of the VMR allows us to build the entirety of dotnet rapidly in a single place.

For additional information, refer to [VMR Code and Build Workflow](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Code-And-Build-Workflow.md).

Another consequence of Codeflow PRs is that the intricate dependency graph among dotnet repositories is replaced by a flat graph of dependencies (also called Flat Flow), where each product subscribes only to updates coming from the VMR.

## Codeflow PR Metadata

The Codeflow mechanism relies on metadata files that can be found in each dotnet repository that is managed by the PCS.
- **`source-manifest.json`**
  - Records the origin of source code changes that are synchronized into the VMR.
- **`Version.Details.xml`**
  - Contains versioning information for dependencies, ensuring consistent package updates.

## What to Do in Case of Conflicts

If a Codeflow PR encounters merge conflicts, follow these steps:
[TBD]

## FAQ

- **Where do packages get built?**
  - Packages are built in Unified Build pipelines. See [UB Pipelines](https://dev.azure.com/dnceng/internal/_build?definitionId=1330).
- **How do I find the new codeflow subscriptions?**
  - A full list of subscriptions can be found at on the [maestro.dot.net](https://maestro.dot.net/subscriptions) webpage. Subscriptions can be managed with DARC commands in the same way they always were.
- **What should I do in case of conflicts?**
  - Follow the steps in [What to Do in Case of Conflicts](#what-to-do-in-case-of-conflicts).

## Additional Resources

- [VMR Code and Build Workflow](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Code-And-Build-Workflow.md)
- [VMR Full Code Flow](https://github.com/dotnet/arcade/blob/main/Documentation/UnifiedBuild/VMR-Full-Code-Flow.md)
- [Darc Documentation](https://github.com/dotnet/arcade/tree/main/Documentation/Darc)

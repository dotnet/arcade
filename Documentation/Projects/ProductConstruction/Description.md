# Product Construction for .NET Core

It represents the infrastructure required to build .NET Core using a multi-repo approach which means that instead of having a centralized system that is going to orchestrate, download each repo that composes the product and build it in specific order, 
the way to build the product will be by automatically flowing dependencies from repos.

Note that for the purposes of this document, the product is defined as: **The set of components required to build the CLI from source.**

## Requirements:
- Must be able to build the product.
- Must be able to trace the set of dependencies that has been shipped or integrated.
- Must be able to quickly propagate changes from individual repos into the final product. 
- Must be able to build individual components from source.
- The Microsoft official build process and the build from source process should be as similar as possible.
- Must be able to ship product components in multiple vehicles with potentially different shipping processes.
- Must be able to service the product.
- Must be able to determine the quality of the product and its components.

## Components:
- Repository tooling and contracts (provided by Arcade) like for example:
  - Repository API common scripts like build.cmd/sh
  - [Dependency Description](https://github.com/dotnet/arcade/blob/master/Documentation/DependencyDescriptionFormat.md)
  - Dependency management (Darc and DarcLib) => more information to come
- Dependency flow automation (Maestro ++) => more information to come
- Visualization/Reporting System (Mission Control and VSTS)

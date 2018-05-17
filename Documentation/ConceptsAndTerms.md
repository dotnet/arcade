# Arcade Concepts and Terms

### Arcade

It's a vehicle to consume, update, and share engineering infrastructure across the .NET Core teams and also a way to convey practices that should be incorporated in .NET Core repos. See [Arcade Overview](Overview.md) for more details.

### Product Build

A build of the Microsoft distribution of the .NET Core product. It encompasses
all supported platforms. It is the composition of all constituent independent
repo builds (e.g. CoreClr, CoreFx, CLI, etc).

###### Alternative terms:

Whole-product Build, Full-stack Build, Coordinated Build

### Source Build


The build that results in source code tarballs that can in turn be built to
provide a community build of .NET Core (strictly speaking this term should be
understood as meaning “source-build build”). The community build can be in turn
distributed and supported by Distribution Maintainers (e.g. Red Hat). Source
Build is a vertical build targeting a specific platform whereas the Product
Build produces components for targeting all supported platforms that is the
Microsoft distributable components of .NET Core. This term should not be
confused with the repo source-build since the source-build repo is the basis for
building both the community build (built from source) and the Microsoft
distributable (Product Build).

###### Alternative terms:

Build from Source

#### Reproducible Build

The ability for isolated repo builds, and in turn the orchestrated and source
builds, to produce byte for byte equivalent output between builds. From
[reproducible-builds.org](https://reproducible-builds.org/):

> Reproducible builds are a set of software development practices that create a verifiable path
>from human readable source code to the binary code used by computers.

###### Alternative terms:

Deterministic Build

### Repo


The source repository for the constituent components that make up the .NET Core
product.

###### Alternative terms:

Component Repo


### Build

Frequently used as a noun representing the output of a compilation process. 
I.e., the output of composing several repos, packages, dependencies in one piece.


### Isolated Repo Build

The build of the Repo independent of the assemblage of the product, ie. The repo
is built with its own declaration of dependencies. The Isolated Repo Build may
leverage pinning of dependency versions to isolate itself from upstream changes.

### Toolset

External or internal tools (such as CMAKE, CLANG, MSBuild, .NET Core SDK,
BuildTools) used in Isolated Repo Builds or Product Builds. Though toolsets are
a dependency, the references to any toolset is from build scripts or targets.
They are not directly included in the product distribution.

### Product Dependency

In the context of either Product Build or Isolated Repo Builds, these are the
artifacts (usually NuGet packages, though not exclusively so), produced by a
repo build that are used by downstream repos, i.e., they are referenced in
downstream repo as a dependency.

### External Dependency

They are the binaries outside of the .NET Core product that are referenced by
repo source code.

### Dependency Flow

In an aggregated build of multiple repos, as each Repo Build is performed in
sequence, dependent artifacts are produced then consumed by downstream repos in
their builds. It is the reification of references declared in repo source code.

### Isolated Dependency Flow

In an isolated repo build, it is the dependency flow wherein each repo
explicitly references specific versions of the artifacts it consumes (pinned),
e.g. auto-upgrade PRs that can be merged to pin a new version of upstream
dependencies.

### Automatic Dependency Flow

In a Product Build, it is the dependency flow wherein each repo references
versions of upstream repo build artifacts that reflect the implicit up-to-date
state of the product. Usually the versions are implicitly the latest produced in
upstream repo builds but there maybe exceptions, such as samples, that reference
pinned prior versions as a product prerogative.

###### Alternative terms:

Auto-dependency flow

### Repo API

​A set of primitive commands common to each repository used to perform typical repository actions. 
[Some examples and description.](https://github.com/dotnet/source-build/blob/dev/release/2.1/Documentation/RepoApi.md)

###### Alternative terms:

Repo Public Surface Area


### Repository Tooling

A set of standalone tools that can be used to interact with the repository. Examples include tools to read repository package information or to update repository package info, etc.

### Build from Source

Download a tarball and just "build it".

### Source Build

Build the interconnected product from source and a base toolset on a single machine disconnected from the Internet.

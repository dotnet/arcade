# Managing SDK Bands

## Purpose

This document describes the problematics of managing multiple .NET SDK bands and discusses how we propose to solve this in the Unified Build world during the .NET 9 timeframe using the new build methodology and the full VMR.

## Terminology

This section presents more precise definitions of common terms used in this document that may be prone to confusion.

- **Individual/Source repository** – One of the current development repositories, e.g., `dotnet/runtime`. An "individual product repository" is then one that contains code for part of the product (but no individual repository contains code to build the whole .NET Core product).
- **VMR (Virtual Monolithic Repository)** – A repository containing code layout that produces the official build product of .NET Core. The repository contains individual product repositories plus tooling to enable a full build of the product.
- **VMR-lite** – present state where changes only flow one way into the VMR which is read-only.
- **Full VMR (Backflow)** – future final state where changes can be made in the VMR too and flow both ways. Dependency flow at that time will be only between VMR and individual repos, not between individual repos themselves.
- **Source-Build** – A set of sources and a process which allows to build the entire product end to end including all its dependencies in offline mode, excluding native dependencies from the source.
- **Microsoft build** – The current build methodology used to assemble the final product.
- **SDK branch** – A git branch related to a specific SDK band, e.g. `release/8.0.1xx`.
- **Non-SDK branch** – A git branch common for all associated SDK bands, e.g. `release/8.0`.
- **Intermediate packages** – Packaged build produces of each of the individual repositories either built in their source repos or during the subsequent build of each individual repository component within the VMR. These are used during package flow between the VMR and the individual repositories.

## SDK bands

Presently, for each major .NET version (e.g., `7.0`), Microsoft not only releases new servicing versions of .NET in succession, but also multiple so-called “bands” of the .NET SDK per each servicing release. These bands differ in their feature set but also share some common parts such as the .NET runtime.  
To best illustrate how this works in practice, let’s imagine the following timeline:

![Example of git history of repositories with shared components and with SDK bands](images/sdk-branches.png)

On the image you can see timelines of branches of two different repositories – `dotnet/sdk` and `dotnet/runtime`. As noted previously, each (servicing) release of .NET contains multiple SDK bands but only one runtime. Each individual repository of each component that needs to differ per band would then have these so-called “SDK branches” named `release/Z.0.Yxx` while repositories that are shared per release have the non-SDK `release/Z.0` branches. As shown in the example, the development of the single runtime would happen in the `release/8.0` branch while the various SDK bands are stored in the following SDK branches (e.g., `release/8.0.1xx` represents the “100th band”).

Once we hit each release day (denoted with red vertical lines), we take the latest commit of each of those branches (that already exist) and release those together. For the releases in the example diagram, this might be:

- Release day #1 – 8.0.0 runtime + one SDK (`8.0.100`)
- Release day #2 – 8.0.6 runtime + two SDKs (`8.0.109`, `8.0.205`)
- Release day #3 – 8.0.12 runtime + three SDKs (`8.0.111`, `8.0.207`, `8.0.302`)

### Band coherence

We call the builds of the SDK bands coherent when the versions of all shared components of each band are the same. To make sure, the bands are coherent and depend on the same version of each non-SDK component (e.g., runtime), we utilize the Maestro dependency flow channels. Non-SDK repositories publish their build products in those channels and the SDK branches consume those. Again, as an example, the following diagram shows a possible setup:

![Example of relationships of repositories with SDK dependency flow channels](images/sdk-channels.png)

This makes sure that eventually the latest version of each shared component (e.g., runtime) flows to all SDK branches which then become coherent between each other.

### Release process

The dependency flow eventually flows all the bits into the `dotnet/installer` repository which also uses the SDK branching. Each of those branches then produces an official build – so one build per band – and we release those. The exact process is that a dedicated person selects all the right official builds which are coherent on the shared bits (so each has the same of the runtime for instance) and inputs the IDs of these builds into the staging pipeline called `Stage-DotNet`. **During this process, it is important for the MSFT build that the shared bits are only built once officially and then re-used in the respective band builds.**

The long-term plan is to transition to building and releasing using the Virtual Monolithic Repository which is a repository where each commit denotes a full set of sources needed for building .NET. The sources of this repository are synchronized from the individual repositories based on the contents of the `dotnet/installer` repository. The goal of this document is to discuss how this will be done with regards to both the different bands as well as the shared components.

### Repository types

Below you can find the split of shared component vs SDK-specific component as it is today.

List of repositories without SDK branches:
- `dotnet/arcade`
- `dotnet/aspnetcore`
- `dotnet/cecil`
- `dotnet/deployment-tools`
- `dotnet/emsdk`
- `dotnet/runtime`
- `dotnet/source-build-externals`
- `dotnet/source-build-reference-packages`

List of repositories with SDK branches:
- `dotnet/command-line-api`
- `dotnet/diagnostics`
- `dotnet/format`
- `dotnet/fsharp`
- `dotnet/installer`
- `dotnet/msbuild`
- `NuGet/NuGet.Client`
- `dotnet/razor - SDK`
- `dotnet/roslyn`
- `dotnet/roslyn-analyzers`
- `dotnet/sdk`
- `dotnet/sourcelink`
- `dotnet/symreader`
- `dotnet/templating`
- `dotnet/test-templates`
- `microsoft/vstest`
- `dotnet/xdt`
- `dotnet/xliff-tasks`

## Proposed solutions

Currently, we end up with SDK branches in the `dotnet/installer` repository and the release process makes sure to package those into the final product. With releasing from the VMR, we have two ways we can approach this:

- **SDK branches**  
    The first obvious solution that comes to mind is to just synchronize the existing SDK branches of `dotnet/installer` into matching branches of the VMR. This is, in fact, what we’re currently already doing with today’s read-only VMR-lite. Each branch/commit of the VMR would then keep producing a single SDK. However, we need to make sure the shared bits are the same in each released SDK branch – we’d say the SDK branches would be coherent then. We also need to make sure that changes made to the shared components in VMR’s SDK branches are flown everywhere appropriately.

- **Side-by-Side folders in the VMR**  
    The second proposed solution would be to take the inverse approach and, instead of having SDK branches, we’d organize VMR’s branches based on the shared bits (e.g. `release/9.0`) and place the different bands of the SDK components side by side in the VMR, e.g. `src/sdk/9.0.1xx`. This makes sure that the shared bits exist only once and each commit of the VMR contains all bands which are coherent.

## Proposal comparison

To compare the two proposals, we identified several areas which might be impacted by the selected architecture, and we’ll try to evaluate how each of the solutions affect them:

- **Build** – what would build of a single and of multiple bands look like with regards to Source Build
- **Code flow** – what does a breaking change mean; how do we (back-)flow the code between the VMR and the individual repositories
- **Developer experience** – impact on developer lives and how they work with the VMR; their options for making changes that span multiple repositories
- **Release** – how do we compile the final Microsoft release
- **Validation** – what do we validate (build/test) and when
- **build their own SDK(s)
- **VMR size & performance** – impact of selected architecture on the git repository
- **Community, 3rd parties & upstream/downstream story** – what does it mean for partners to 
- **Implementation and maintenance complexity** – risks and costs associated with the future

### Build

The current (Microsoft) way of building the SDKs is based on re-using previously built artifacts which come into the build as NuGet packages. The components are flown as already compiled packages. This means that when building each SDK band, we only restore the shared components which were built only once at some point in the past during the official build of their source repository.

In .NET 9.0, when the full VMR code flow is in place (see [VMR Code and Build Workflow](./VMR-Code-And-Build-Workflow.md) for details), we’ll be building the individual repositories on more occasions:

- During the official build of their source repository – this will use other repo’s intermediate packages whenever they depend on another repo as if they were just built from source.
- During the official build of the VMR – when we build the whole product from source. This will end up producing an intermediate package per each individual repository built as part of the whole build.

This means that at several places, we’ll be building both the shared components and the SDK band components from source where their dependencies will be either freshly built or restored in a local NuGet cache of the build machine.

Regardless of how this will happen, there’s no real difference whether we’d build the SDK bands from folders which are side by side in a folder of a checked-out branch or which are checkouts of different SDK branches. The build process would need to know about the existence of multiple SDK bands eventually placed in a folder “somewhere on the disk”. **This means that the selected architecture doesn’t really affect the build and we don’t need to consider it during evaluation.** There will, however, be a change to the build process needed to accommodate for the new locations of the bands.

The various situations can be summarized as follows:
- For individual repository builds, the build process will need to know about the location of the SDK bands. This is the same for both proposals.
- For the VMR build shared components are built with the first band and put in a local NuGet cache. Other bands restore shared components from the cache (or rebuild the components over of cache is not possible).

### Code flow

Code flow is where the two approaches differ dramatically. The biggest difference is during breaking changes in shared components and how/when these get resolved. To re-iterate what the code flow will look like in .NET 9 time frame with full VMR back flow – the individual repositories only receive and send updates from/to the VMR and not between each other, so the situation looks like this (see [VMR Code and Build Workflow](./VMR-Code-And-Build-Workflow.md) for details):

```mermaid
flowchart TD
    VMR[VMR]
    Runtime[runtime]
    Roslyn[roslyn]
    MSBuild[msbuild]
    FSharp[fsharp]
    Other[...]

    Runtime-->VMR
    Roslyn-->VMR
    MSBuild-->VMR
    Other-->VMR
    FSharp-->VMR
    VMR-->Runtime
    VMR-->Roslyn
    VMR-->MSBuild
    VMR-->Other
    VMR-->FSharp
```

For a simple forward flow where a shared component is changed, the code flow needed to update all branches does not differ much.

SDK branches:

```mermaid
sequenceDiagram
    autonumber

    participant runtime as dotnet/runtime<br />release/9.0
    participant SDK_1xx as dotnet/sdk<br />release/9.0.1xx
    participant SDK_2xx as dotnet/sdk<br />release/9.0.2xx
    participant VMR_1xx as VMR<br />release/9.0.1xx
    participant VMR_2xx as VMR<br />release/9.0.2xx

    Note over runtime, VMR_2xx: Initial state - all changes flown<br />dotnet/runtime 9.0 @ RUN_1<br />dotnet/sdk 1xx @ SDK_1.1 (RUN_1)<br />dotnet/sdk 2xx @ SDK_2.1 (RUN_1)<br />VMR 1xx @ VMR_1.1 (SDK_1.1, RUN_1)<br />VMR 2xx @ VMR_2.1 (SDK_2.1, RUN_1)

    runtime->>runtime: New change ➡️ RUN_2

    runtime->>VMR_1xx: Flow of 📄 RUN_2 ➡️ VMR_1.2
    Note over VMR_1xx: 📦 Runtime intermediate 2 is built
    runtime->>VMR_2xx: Flow of 📄 RUN_2 ➡️ VMR_2.1
    Note over VMR_2xx: 📦 Runtime intermediate 3 is built

    Note over VMR_2xx: ✅ Coherent state<br />VMR 1xx and 2xx have 📄 RUN_2

    # Backflow
    VMR_1xx->>SDK_1xx: Backflow of 📦 RUN_IP_2 ➡️ SDK_1.2
    SDK_1xx->>VMR_1xx: Flow of 📄 SDK_1.2
    Note over VMR_1xx: 📦 SDK 1xx intermediate is built
    VMR_2xx->>SDK_2xx: Backflow of 📦 RUN_IP_3 ➡️ SDK_2.2
    SDK_2xx->>VMR_2xx: Flow of 📄 SDK_2.2
    Note over VMR_2xx: 📦 SDK 2xx intermediate is built
```

Side-by-side folders:


```mermaid
sequenceDiagram
    autonumber

    participant runtime as dotnet/runtime<br />release/9.0
    participant SDK_1xx as dotnet/sdk<br />release/9.0.1xx
    participant SDK_2xx as dotnet/sdk<br />release/9.0.2xx
    participant VMR as VMR<br />release/9.0

    Note over runtime, VMR: Initial state - all changes flown<br />dotnet/runtime 9.0 @ RUN1<br />dotnet/sdk 1xx @ SDK1 / RUN1<br />dotnet/sdk 2xx @ SDK2 / RUN1<br />VMR 9.0 @ VMR1 / SDK1+SDK2, RUN1

    runtime->>runtime: Change in runtime
    runtime->>VMR: Flow of 📄 RUN_2
    Note over VMR: 📦 Runtime intermediate 2 is built
    VMR->>SDK_1xx: Backflow of 📦 RUN_IP_2
    SDK_1xx->>VMR: Flow of 📄 SDK_1.2
    Note over VMR: 📦 SDK 1xx intermediate is built
    VMR->>SDK_2xx: Backflow of 📦 RUN_IP_2
    SDK_2xx->>VMR: Flow of 📄 SDK_1.2
    Note over VMR: 📦 SDK 2xx intermediate is built
```

The situation gets more interesting for breaking changes. Let’s imagine a situation where a change is needed in one of the bands that requires a breaking change in a shared component. For this, we assume that a change like this would be always made in the VMR where we can change both components at the same time.

SDK branches:

```mermaid
sequenceDiagram
    autonumber

    participant runtime as dotnet/runtime<br />release/9.0
    participant SDK_1xx as dotnet/sdk<br />release/9.0.1xx
    participant SDK_2xx as dotnet/sdk<br />release/9.0.2xx
    participant VMR_1xx as VMR<br />release/9.0.1xx
    participant VMR_2xx as VMR<br />release/9.0.2xx

    Note over runtime, VMR_2xx: Initial state - all changes flown<br />dotnet/runtime 9.0 @ RUN_1<br />dotnet/sdk 1xx @ SDK_1.1 (RUN_1)<br />dotnet/sdk 2xx @ SDK_2.1 (RUN_1)<br />VMR 1xx @ VMR_1 (SDK_1.1, RUN_1)<br />VMR 2xx @ VMR_2 (SDK_2.1, RUN_1)

    # Change in SDK+runtime
    VMR_1xx->>VMR_1xx: Change in runtime ➡️ SDK_1.2, RUN_2
    Note over VMR_1xx: 📦 Runtime intermediate 2 is built<br />📦 SDK 1xx intermediate is built

    # Backflow
    VMR_1xx->>runtime: Backflow of 📄 RUN_2
    VMR_1xx->>SDK_1xx: Backflow of 📄 SDK_1.2, 📦 RUN_IP_2 ➡️ SDK_1.3

    runtime->>VMR_2xx: Flow of RUN_2
    activate VMR_2xx
    Note over VMR_2xx: ❌ Requires change in 2xx
    Note over VMR_2xx: 📦 Runtime intermediate 3 is built<br />📦 SDK 2xx intermediate is built

    deactivate VMR_2xx

    Note over VMR_2xx: ✅ Coherent state<br />VMR 1xx and 2xx have 📄 RUN_2

    VMR_2xx->>SDK_2xx: Backflow of 📄 SDK_2.2, 📦 RUN_IP_3 ➡️ SDK_2.2
    SDK_2xx->>VMR_2xx: Flow of 📄 SDK_2.2
    Note over VMR_2xx: 📦 SDK 2xx intermediate is built
```

Side-by-side folders:

```mermaid
sequenceDiagram
    autonumber

    participant runtime as dotnet/runtime<br />release/9.0
    participant SDK_1xx as dotnet/sdk<br />release/9.0.1xx
    participant SDK_2xx as dotnet/sdk<br />release/9.0.2xx
    participant VMR as VMR<br />release/9.0

    Note over runtime, VMR: Initial state - all changes flown<br />dotnet/runtime 9.0 @ RUN1<br />dotnet/sdk 1xx @ SDK1 / RUN1<br />dotnet/sdk 2xx @ SDK2 / RUN1<br />VMR 9.0 @ VMR1 / SDK1+SDK2, RUN1

    activate VMR
    VMR->>VMR: New change of runtime + SDK 1xx
    Note over VMR: ❌ PR fails - Requires change in 2xx too
    Note over VMR: Fix is made immediately
    Note over VMR: 📄 RUN1, SDK_1.2 and SDK_2.2<br />📦 Runtime intermediate 2 is built
    deactivate VMR

    # Backflow
    VMR->>runtime: Backflow of 📄 RUN_2
    VMR->>SDK_1xx: Backflow of 📄 SDK_1.2, 📦 RUN_IP_2
    VMR->>SDK_2xx: Backflow of 📄 SDK_2.2, 📦 RUN_IP_2
```

From the above, the side-by-side solution is much more resilient to breaking changes as those need to be dealt with immediately. The VMR won’t ever get into an inconsistent state as the bands live within a single commit. Whereas in the SDK folder solution, the breaking change is created and is dealt with in a follow-up step once code flows to the branch of the other band.

Other difference is in the number of steps in the flow to reach a coherent state. This is lower for side-by-side as incoherency is impossible from the start and the system does not need to deal with it. The number of changes needed is not that much higher though as we still need to flow changes to the same number of branches of all individual repositories that are part of the change. This does not differ much whether we flow folders from one or more branches.

### Developer experience

Important area to consider is how the day-to-day interactions of .NET developers with the VMR would look like. We identified the following key actions the developers are interested in:

- **Working with sources** – working with the source files such as searching, usual investigations such as checking file history & looking for symbols, and finally code editing and building itself
- **Git operation complexity** – actions such as checking file history, diffing bands, backporting changes between bands..
- **Git operation performance** – duration of operations such as `git status`. This area is considered separately [later in the document](#vmr-size--performance).

That said, it’s important to realize that most of the work and the VMR is the most active in the preview time where we only have one SDK band.

It is obvious that the SDK branch proposal wins in most of these categories. It stays truer to git by using commits/branches for file versioning rather than folders with versions in their name as it is with the side-by-side layout. This works well with all kinds of tooling and workflows:

- File history breaks with side-by-side folders once we snap the bands.
- File and symbol search might be confusing when going through almost identical side-by-side folders.
- Backporting a change between bands sounds less error prone with SDK branches as it’s just porting commits. Making a change in several bands at once can lead to omitting a change in one of the bands, harder-to-review pull requests and overall it seems to be easier to transfer changes between bands using git-native approaches. Of course it’s possible to utilize patches but once a change is up for review, making sure all bands stay in sync might be problematic.
- Diffing bands sounds easier to do with SDK branches as well as you don’t need to worry which all components are shared and which are not – diffing side-by-side folders might become tedious once we need to compare several repositories at once.

Some points of interested are rather a matter of personal preference – is it better for a developer to make a change in one SDK branch, open a PR and then backport to other branches, or is it better to do everything at once and build/validate it together? But over all it seems that storing code based on how we work with it rather than how we release it sounds better and the SDK branches proposal seems as the superior approach in this area since it optimizes for development rather than bend the architecture/layout to how we release the product.

### Release

> TODO
> -	Separating development and release cycle – the fact that we release several SDKs is a Microsoft problem that 3rd parties may or may not care about. We shouldn’t enforce the problems of our custom release on others through the code layout – for instance, you shouldn’t be forced to check out multiple versions of the code if you don’t care about multiple bands. Further, we shouldn’t tax the .NET team itself and subordinate how we store code to how we release it later.
> -	Note: Resiliency to band explosion – keeping bands in branches seems more resilient to outer requirements such as a sudden increase in the number of bands due to Visual Studio speeding up its release cycle.

### Validation

By validation we mean the process of running some set of tests over a changed code. For individual repositories, there is no change compared to today. There is a difference though for changes made directly in the VMR as we need to specify what needs to be built and validated.

It is unclear what the validation story should be when we have all SDKs in folders side-by-side and how it would impact developers. On one hand, validating a shared component change in all SDK branches adds up to more compute time as the shared components will get re-built in each branch. On the other hand, building all non-shared components always impacts every build and that might have negative impact on developer productivity.

> TODO: Missing table

> TODO: Diagram showing the break

### VMR size & performance

By size and performance we mean the implications of each proposed repository layout onto these metrics onto few key metrics that affect common operations we do with the VMR.

#### Git repository size

The expectation is that the size of the overall repository won’t differ much between the two proposed layouts. This is due to the mechanism of how git stores data internally. For illustration of this expectation, imagine a situation where we have two SDK bands which differ in one file only – `src/sdk/foo/bar.txt`. Let’s look at what is inside of the git object database:

- For SDK branches, we have the two commits that describe the two branches. These are equal at first, when we freshly snap the band branches.
- For side-by-side folders, the situation isn’t much different with the two git trees for each of the band folders – `src/sdk/9.0.1xx` and `src/sdk/9.0.2xx`. These are also the same at first – only the parent tree representing the sdk folder is an object created after the snap.
- 
Once we change bar.txt, we’d get a new object for the file itself and then some other:

- For SDK branches, we have the new git trees that describe the parent folders of the changed file leading up to the root of the repository (`/` → `src` → `sdk` → `foo`).
- For side-by-side folders, the situation is again not much different – we get a new set of git trees describing the path from foo through the new `src/sdk/9.0.2xx`.

Looking at this simple example, it hints that whatever solution we pick, the number of git objects we’ll create to capture the changes are around the same.

#### Single SDK source tarball size

Another interesting metric is the archive of sources needed to build a single SDK. It seems that when 3rd parties that only care about one SDK band would be downloading sources of VMR commits, the side-by-side layout incurs quite a big toll on the overall size as we’d need to download the sources of all bands always. The release process might be customized, and we could omit other bands from the tarball.

#### Release source tarball size

By release source tarball we mean an archive of all sources needed to build a whole release containing several SDK bands.
For side-by-side folders, this would simply equal to a VMR commit. For SDK branches, we’d have to do something about compiling the release archive as the shared components need to appear in the tarball just once.

> TODO: It’s a question whether there would be a thing such as “tarball for the whole release with all bands” but it’s probably possible to exclude the shared components from all but one band and reach the same result with SDK folder as with SDK branches.

#### Git operation performance

We care about the duration of common git operation duration such as checkout or status. We also need to consider scenarios in which we use the VMR – do we care about more bands than one?

SDK branches seem to have the innate benefit of not having to check out all the bands always. This seems like a big win for scenarios where we for instance make changes to one band only. But it goes away quite quickly once we need to build all of them and suddenly the checkout takes much longer.

> TODO: We should figure out how often would we care about building all bands in various flows – from CI to inner dev loop.

#### Summary of git size/performance

From the analysis above, it seems that to declare a winner, we need to consider how often we deal with a single vs multiple bands. Both solutions are a good fit for one or the other, never both.

> TODO: Missing table

### Community, 3rd parties & upstream/downstream story

There are quite big implications of how we lay the bands out in the VMR. 3rd parties consuming .NET might or might not care about building multiple bands. Overall, the fact that we even need to have different SDK bands is native to Microsoft’s rhythm and way of bundling releases.

For SDK branches, nothing really changes in this regard as you can keep building the branch as you were doing until now and get the SDK you care about.  
For side-by-side, the situation is quite different. We’re suddenly influencing everyone’s experience with the VMR by projecting how we bundle releases into the layout of the code. This has negative implications such as having to check out all the bands always which would for instance prolong all repo operations.

> TODO: Forks and upstreaming changes differences? Some things are common for both, such as confusion about where a change should go but that’s the same for folders/branches.

### Implementation and maintenance complexity

- TODO: Much more work to implement side-by-side
- TODO: Resiliency to band explosion – keeping bands in branches seems more resilient to outer requirements such as a sudden increase in the number of bands due to Visual Studio speeding up its release cycle.

### Comparison summary

## Comparison evaluation

Both approaches seem to have pros and cons. To choose the best approach, we should assign importance to the evaluation areas on which we were comparing these and see which approach seems better.

When doing so, we should take into account the product lifecycle. At first, the most active busy development happens in the preview time (on main branches). Only after the release, we move into servicing and only after then we branch out and snap the bands. We expect the servicing period to last very long but with less activity. During active development, we should prioritize **developer experience** and **code flow** as that has impact on product construction.  
During servicing we need the system to be as frictionless as possible so that we’re able to react to external impulses fast and release fixes fast which hints at prioritizing **code flow**, **release**, and **maintenance complexity**. Some areas should be important in both periods such as **community impact**.

> TODO: Actually decide


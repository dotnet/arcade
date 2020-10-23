# Arcade Servicing

This document is intended to describe Arcade servicing workflow and policies.
Our goal is to ensure that supported versions of the .NET Core product remain
serviceable over time. As the Engineering Services team, we will need to make
major changes to Arcade and our services over time to support new versions of
.NET Core, while still supporting older versions.

Mechanically, this is done in two ways:
- Branch our tools (Arcade) with the product.
- Rev the APIs of our services as needed.

**For those inserting into Visual Studio, careful consideration should be given to which Arcade to take a dependency on.  If latest features are needed, then 'n' (or master) might make sense, otherwise 'n-1' (servicing) will be more stable and have much less churn.**

**It's important to note that we *must* continue to validate any supported versions
of Arcade or services.**

## Details

### Where do we do work on Arcade SDK?
- `master` for future releases
- `release/<NET Core Major Version Number>` for servicing

### How do I get my servicing fix into master?
- Fixes that apply to both master as well as servicing releases must be first checked into
  master and then cherry picked into the appropriate servicing branches.
- Code flow happens from servicing branches back into master for the purposes of completeness,
  but developers must **not** rely on this to get fixes into master.

### When do we branch?
- Major releases of .NET Core, not minor

### What do we 'branch'?
- **Yes**: Arcade repo (templates, SDK, etc.)
- **As Needed**: Services should 'branch' as needed via API versioning to maintain
    compatibility for versions used in various servicing releases.
- **No**: OSOB images do not branch, but old images must be able to be resurrected.

### What is the bar for changes in servicing branches of Arcade?
- Uses same bar as product servicing, where our customers are the repositories.
    - Security
    - Exceptionally high impact issues reported by customers.
    - External dependencies change.
- See [Change Bar](./ChangeBar.md) and [Changes Policy](./ChangesPolicy.md) for
  additional details on change bars and communication policies.

### When can Arcade servicing changes be merged?
Arcade servicing changes may be merged when product branches are open. When product branches are not open, they may be merged in very special cases (e.g. targeted fixes for specific repositories, non-aligned servicing schedules, etc.). Otherwise, they should wait in PR until the product branches open for the next round of serivcing fixes.

### How do Arcade servicing changes flow to repositories

Arcade servicing changes flow like any other product change, through dependency flow. These subscriptions shall have two states:
- Only triggered on demand
- Triggered on every build of arcade.
After branches open for a servicing release, servicing subscriptions shall be set to flow every build. This ensures that any set of changes checked in that timeframe will flow as quickly to repositories as possible. When branches close for the stabilization and coherency process, those subscriptions shall be set to flow only on demand to reduce risk that an accidental merge to the servicing branch can reset the coherency process. The coherency QB may choose to flow changes selectively (e.g. ones approved in tactics to get build ready) during this timeframe.

It is the repsonsibility of the coherency QB to ensure that the correct changes are merged, the correct updates flow, and the subscriptions are in the correct state.

### What is the bar for changes affecting non-current (not latest) APIs in engineering services?
- Uses same bar as product servicing, where our customers are the repositories.
    - Security
    - Exceptionally high impact issues reported by customers.
    - External dependencies change.
- See [Change Bar](./ChangeBar.md) and [Changes Policy](./ChangesPolicy.md) for
  additional details on change bars and communication policies.

## Mechanics of branching Arcade and services?

The mechanics of 'branching' our services tends to be service specific, but
generally involves generating new API versions for breaking changes. For Arcade,
the mechanics are a little more complex. The following is the process by
which Arcade can be branched for major release 'N' of .NET Core.

1. Branch `dotnet/arcade` off of `master` into `release/<N>`
2. Branch `dotnet/arcade-validation` off of `master` into `release/<N>`
3. Update package version numbers in master to match the next major version of
   .NET (N+1). ([example](https://github.com/dotnet/arcade/pull/6356/files))
4. [Introduce channels](https://github.com/dotnet/arcade/blob/master/Documentation/Darc.md#add-channel)
   for the new branches, classified (`-c`) as `tools`
    - `.NET <N or next version> Eng`
    - `.NET <N or next version> Eng - Validation`
5. [Add default channel associations](https://github.com/dotnet/arcade/blob/master/Documentation/Darc.md#add-default-channel)
   for Arcade `release/<N>` to point to `.NET <N> Eng - Validation`
6. [Create a subscription](https://github.com/dotnet/arcade/blob/master/Documentation/Darc.md#add-subscription) from `arcade`  to `arcade-validation` (branch `release/<N>`) to take changes from the `.NET <N> Eng - Validation` channel.
7. Modify the new release branch  `release/<N>` of arcade-validation to promote builds
   to `.NET <N> Eng`. ([example](https://github.com/dotnet/arcade-validation/pull/1857/files))
8. Update [PublishingConstants.cs](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Build.Tasks.Feed/src/model/PublishingConstants.cs)
   in Arcade's master for new channels ([example](https://github.com/dotnet/arcade/pull/6360/files))
9. Reset arcade Maestro++ subscriptions targeting .NET release branches to
   source from `.NET <N> Eng`.

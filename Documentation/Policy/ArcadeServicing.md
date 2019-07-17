# Arcade Servicing

This document is intended to describe arcade servicing workflow and policies.
Our goal is to ensure that supported versions of the .NET Core product remain
servicable over time. As the Engineering Services team, we will need to make
major changes to Arcade and our services over time to support new versions of
.NET Core, while still supporting older versions.

Mechanically, this is done in two ways:
- Branch our tools (Arcade) with the product.
- Rev the APIs of our services as needed.

**It's important to note that we *must* continue to validate any supported versions
of Arcade or services.**

## Details

### Where do we do work on Arcade SDK?
- `master` for future releases
- `release/<NET Core Major Version Number>` for servicing

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
- See [Change Bar](#ChangeBar.md) and [Changes Policy](#ChangesPolicy.md) for
  additional details on change bars and communication policies.

### What is the bar for changes affecting non-current (not latest) APIs in engineering services?
- Uses same bar as product servicing, where our customers are the repositories.
    - Security
    - Exceptionally high impact issues reported by customers.
    - External dependencies change.
- See [Change Bar](#ChangeBar.md) and [Changes Policy](#ChangesPolicy.md) for
  additional details on change bars and communication policies.

## Mechanics of branching Arcade and services?

The mechanics of 'branching' our services tends to be service specific, but
generally involves generating new API versions for breaking changes. For Arcade,
the mechanics are a little more complex. The the following is the process by
which Arcade can be branched for major release 'N' of .NET Core.

1. Branch `dotnet/arcade` off of `master` into `release/<N>`
2. Branch `dotnet/arcade-validation` off of `master` into `release/<N>`
3. Update package version numbers in master to match the next major version of
   .NET Core (N+1).
4. Introduce channels for the new branches
    - `.NET Core <N+1 or next version> Tools`
    - `.NET Core <N+1 or next version> Tools - Validation`
5. Modify default channel associations for Arcade `master` to point to `.NET Core <N+1
   or next version> Tools - Validation`
6. Add default channel associations for Arcade `release/<N>` to point to `.NET
   Core <N> Tools - Validation`
7. Modify the release and master branches of arcade-validation to promote builds
   to the appropriate channels.
8. Reset arcade Maestro++ subscriptions targeting .NET Core master branches to
   source from .NET Core <N+1> Tools.
9. Reset arcade Maestro++ subscriptions targeting .NET Core release branches to
   source from .NET Core <N> Tools.
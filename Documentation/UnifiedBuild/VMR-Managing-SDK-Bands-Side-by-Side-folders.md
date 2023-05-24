> Note: This is a proposal for a strategy to build, manage and release multiple SDK bands of .NET. The proposal is part of the [Unified Build](./README.md) effort. For more context about the problem this design is trying to solve see the [Managing SDK Bands](./VMR-Managing-SDK-Bands.md) document.

# Managing SDK Bands - "Side by Side folders" proposal

This proposed solution would be to take the inverse approach and, instead of having SDK branches in the VMR, we’d organize the branches based on the shared bits (e.g. `release/9.0`). We would then place the band specific components side-by-side in folders.

This document describes the end-to-end process from developing to shipping multiple SDK bands using this model.

## Layout

For simplicity, let's consider we are synchronizing the repositories `dotnet/arcade`, `dotnet/runtime`, `dotnet/roslyn` and `dotnet/sdk` where `dotnet/runtime` and `dotnet/arcade` are the shared components.

Layout of files in the VMR would be as follows:

```
/
└── src
    ├── roslyn
    ├── sdk
    └── shared
        ├── arcade
        └── runtime
```

The layout has the following characteristics:

- Each repository is a folder either under `src/` or `src/shared/` in the VMR.
- VMR has branches for each major .NET version, e.g. `release/9.0`.
- Each commit of the VMR contains code for all SDK bands with shared components having a single copy.

> TODO: ❓❓❓ What does a single band VMR look like? Single band VMR is in the `main` where we develop preview version of .NET.

## Code flow

## Build

## Band snap

## Release
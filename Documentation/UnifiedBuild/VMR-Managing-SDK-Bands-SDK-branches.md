> Note: This is a proposal for a strategy to build, manage and release multiple SDK bands of .NET. The proposal is part of the [Unified Build](./README.md) effort. For more context about the problem this design is trying to solve see the [Managing SDK Bands](./VMR-Managing-SDK-Bands.md) document.

# Managing SDK Bands - "SDK branches" proposal

This proposal is probably the most obvious solution that comes to mind considering where we are today. The bottom line is that we'd just keep using SDK branches in the VMR the same way we have them today. This is, in fact, what we’re currently already doing with today’s read-only VMR-lite where we synchronize the SDK branches of `dotnet/installer`.

This document describes the end-to-end process from developing to shipping multiple SDK bands using this model.

## Layout

For simplicity, let's consider we are synchronizing the repositories `dotnet/arcade`, `dotnet/runtime`, `dotnet/roslyn` and `dotnet/sdk` where `dotnet/runtime` and `dotnet/arcade` are the shared components.

The layout of files stays the same as today's VMR-lite:

```
/
└── src
    ├── arcade
    ├── roslyn
    ├── runtime
    └── sdk
```

The layout has the following characteristics:
- Each repository is a folder under `src/` in the VMR.
- VMR has SDK branches, e.g. `release/9.0.1xx` and `release/9.0.2xx`.
- Each branch has a full copy of all repositories, even shared ones.
- Each commit of the VMR produces a single runtime and single SDK.

## Code flow

## Build

## Band snap

## Release

> TODO - note: However, we need to make sure the shared bits are the same in each released SDK branch – we’d say the SDK branches would be coherent then. We also need to make sure that changes made to the shared components in VMR’s SDK branches are flown everywhere appropriately.

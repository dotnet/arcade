# Proposal: Support older .NET SDKs with Arcade `main`

**Status**: Proposal / Request for comment
**Audience**: Arcade, dnceng, VMR stakeholders

## 1. Summary

Today, Arcade `main` is effectively coupled to the in-development .NET SDK. This forces every repository subscribed to Arcade's dependency-flow channel to move its `global.json` SDK in lockstep with Arcade `main`, which in turn makes cross-cutting changes to Arcade or `eng/common` in the VMR expensive: every inner repo must be on a compatible SDK before the change can land.

This document proposes three coordinated changes that, taken together, allow Arcade `main` to be consumed by any repo on a supported .NET SDK (at minimum the current and previous major release — today .NET 11 and .NET 10):

1. Move .NET SDK updates in consumer repos to [Dependabot](https://devblogs.microsoft.com/dotnet/using-dependabot-to-manage-dotnet-sdk-updates/) and stop flowing the SDK version through the Product Construction Service (PCS) via Arcade's dependency-flow channel. Repos can opt out of PCS-driven SDK updates today by setting `tools.pinned: true` in `global.json`; in parallel, `global.json` can be simplified to use only `sdk.version` (no duplicate `tools.dotnet`), which requires a small `eng/common` change.
2. Redefine the floating TFM properties (`NetCurrent`, `NetPrevious`, `NetMinimum`, `NetFrameworkMinimum`) so their values are no longer tied to a specific Arcade release branch.
3. Keep Arcade `main` and `eng/common` working on the current and previous major .NET SDK versions, going forward.

A first piece of (3) — moving Arcade's own project `TargetFramework` values from `$(NetCurrent)` down to `$(NetMinimum)` so that consumer repos on the previous-major SDK can load Arcade's tools and MSBuild tasks — has already merged on `main` ([dotnet/arcade@98d7ce0](https://github.com/dotnet/arcade/commit/98d7ce08e83c980d2bcc30bf3c846c8b3630c391)).

## 2. Background — current state

### 2.1 SDK version flow

Arcade's `global.json` pins the in-development SDK, e.g.:

```json
{
  "sdk": {
    "version": "11.0.100-preview.4.26210.111",
    "rollForward": "latestFeature"
  },
  "tools": {
    "dotnet": "11.0.100-preview.4.26210.111"
  }
}
```

(See [`global.json`](../global.json).)

That SDK version is then flowed by PCS to every repository subscribed to Arcade's dependency-flow channel, alongside Arcade SDK package versions. The practical effect is that any repo consuming Arcade `main` also receives the latest in-development .NET SDK.

### 2.2 Floating TFM properties

Arcade defines a set of "floating" TFM properties in
[`src/Microsoft.DotNet.Arcade.Sdk/tools/TargetFrameworkDefaults.props`](../src/Microsoft.DotNet.Arcade.Sdk/tools/TargetFrameworkDefaults.props):

```xml
<NetCurrent>net11.0</NetCurrent>
<NetPrevious/>
<NetMinimum>net10.0</NetMinimum>
<NetFrameworkCurrent>net481</NetFrameworkCurrent>
<NetFrameworkMinimum>net462</NetFrameworkMinimum>
<NetFrameworkToolCurrent>net472</NetFrameworkToolCurrent>
```

These are documented in [`Documentation/ArcadeSdk.md`](./ArcadeSdk.md) as:

- `NetCurrent` — The TFM of the major release of .NET that the Arcade SDK aligns with.
- `NetPrevious` — The previously released version of .NET.
- `NetMinimum` — Lowest supported version of .NET at the time of the release of `NetCurrent`.
- `NetFrameworkMinimum` — Lowest supported version of .NET Framework at that time.

The values are hard-coded per branch:

| Arcade branch  | `NetCurrent` | `NetPrevious` | `NetMinimum` |
|----------------|--------------|---------------|--------------|
| `main`         | `net11.0`    | (unset)       | `net10.0`    |
| `release/10.0` | `net10.0`    | `net9.0`      | `net8.0`     |

That is: the meaning of `$(NetCurrent)` in a consuming project depends on which Arcade branch happens to be feeding it, not on what the consuming repo is actually doing.

### 2.3 Arcade's own TFMs (already addressed)

Until recently, several Arcade projects (`Microsoft.DotNet.Arcade.Sdk`, helix, build tasks, signtool, etc.) targeted `$(NetCurrent)`. A change has merged on `main` that downgrades these projects to `$(NetMinimum)`: [dotnet/arcade@98d7ce0](https://github.com/dotnet/arcade/commit/98d7ce08e83c980d2bcc30bf3c846c8b3630c391).

The motivation was not to allow Arcade itself to be built with an older SDK, but to let consumer repositories that depend on Arcade tools and MSBuild tasks load those assemblies on an older .NET SDK and runtime. With Arcade's tasks compiled against `$(NetMinimum)` (currently `net10.0`) instead of `$(NetCurrent)` (currently `net11.0`), a repo on the previous-major SDK can still load and run them.

This proposal builds on top of that work — it does not duplicate it.

## 3. Problems

Even with Arcade's own TFMs already at `$(NetMinimum)`, consumers of Arcade `main` are still tied to the in-development SDK because:

1. **PCS flows the SDK version.** Repos subscribed to Arcade's dependency-flow channel receive Arcade's `global.json` SDK alongside Arcade package versions, so an SDK uplift is forced regardless of whether the repo wants it.
2. **`NetCurrent`/`NetPrevious`/`NetMinimum` change meaning with the Arcade branch.** A project that uses `<TargetFramework>$(NetCurrent)</TargetFramework>` retargets simply because the upstream Arcade branch moved on. That is desirable for some repos and surprising for others.
3. **Cross-cutting VMR changes are expensive.** When building inside the VMR, every inner repo is built against Arcade `main`. But the same inner repos in their standalone (non-VMR) builds may consume a different Arcade — typically because they are pinned to a specific .NET SDK (e.g. .NET 10) that Arcade `main` doesn't support, so they pick up Arcade `release/10.0` instead. This is independent of which release band the repo ships in: a current-band (.NET 11) repo can still be on Arcade `release/10.0` because of its SDK pin. A cross-cutting change to Arcade plus its consumers therefore can't simply be made in `main` and one PR per repo — the change has to work against whatever Arcade each repo uses standalone, which often means coordinated changes across multiple Arcade branches and inner repos before anything can flow.

## 4. Proposal

### 4.1 Move SDK updates to Dependabot; stop PCS SDK flow

Repos should own their own `global.json` SDK version. Updates are picked up by [Dependabot for .NET SDK updates](https://devblogs.microsoft.com/dotnet/using-dependabot-to-manage-dotnet-sdk-updates/), which is the upstream-supported way to keep the SDK current. Arcade stops pushing its `global.json` SDK version to subscribed repos.

**Recommendation**: stop the PCS-driven SDK flow.

**Fallback**: if there is product-level pressure to keep some repos on a specific SDK in lockstep with Arcade, the current PCS mechanism can be retained as an opt-in for those repos. The expectation, however, is that this becomes the exception, not the default.

#### Simplifying `global.json` and per-repo pin

Two related but separate changes are needed here:

**Change A — Drop `tools.dotnet`; rely on `sdk.version`.** Today, Arcade's `global.json` carries both an `sdk.version` and a duplicate `tools.dotnet` entry:

```json
{
  "sdk": { "version": "11.0.100-preview.4.26210.111", "rollForward": "latestFeature" },
  "tools": { "dotnet": "11.0.100-preview.4.26210.111" }
}
```

`tools.dotnet` predates `sdk.version`-aware bootstrap behavior and is now redundant for most purposes. A repo can drop it and keep only `sdk.version`, as demonstrated by [`NuGet/NuGet.Client`'s `global.json`](https://github.com/NuGet/NuGet.Client/blob/e69ab5e626d307d35f29c3250225a2e12305f038/global.json#L4).

For this to be usable everywhere, **`eng/common` must support a `global.json` that defines `sdk.version` but no `tools.dotnet`**. Today, the bootstrap reads `tools.dotnet` and errors out if it is absent (see [`eng/common/tools.ps1`](../eng/common/tools.ps1) line ~187 and the explicit error at line ~571: *"/global.json must specify 'tools.dotnet'."*). The proposed change to `eng/common`:

- If `tools.dotnet` is set, behave exactly as today.
- Otherwise, fall back to `sdk.version` to determine which SDK to restore and use for the build.
- Apply the same fallback in the sibling `tools.sh`, `dotnet-install.ps1`/`.sh`, and `InstallDotNetCore.cs`/`.targets` code paths.

Note: dropping `tools.dotnet` on its own does **not** stop PCS from updating `sdk.version` — PCS will still flow the SDK version into whichever entry is present. Change A is about simplifying `global.json` and removing the duplicate entry, not about opting out of flow.

**Change B — Pin via `tools.pinned: true`.** To actually opt out of PCS-driven SDK updates, a repo adds:

```json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "tools": { "pinned": true }
}
```

PCS honors `tools.pinned: true` and will not update the SDK version in that repo's `global.json`. This is the supported per-repo opt-out and works independently of Change A. A repo can apply it today, ahead of any global change to PCS behavior, as a low-risk way to decouple its SDK cadence from Arcade's.

Open question: do we want a transition period where both mechanisms coexist, and what is the off-ramp? `tools.pinned` makes a long coexistence cheap, since opt-out is a one-line repo change.

### 4.2 Redefine the floating TFM properties

The goal is to make `NetCurrent`/`NetPrevious`/`NetMinimum` mean the same thing in Arcade `main`, `release/10.0`, `release/9.0`, etc., so that a project author does not have to reason about "which Arcade branch am I being built by".

#### Option A — Derive from the consuming SDK version

`NetCurrent` etc. are computed from the major version of the SDK in the consuming repo's `global.json`:

| SDK in `global.json` | `NetCurrent` | `NetPrevious` | `NetMinimum` |
|----------------------|--------------|---------------|--------------|
| `10.x.x`             | `net10.0`    | `net9.0`      | `net8.0`     |
| `11.x.x`             | `net11.0`    | `net10.0`     | `net10.0`    |

This is straightforward and aligns the meaning of "current" with what the repo is actually building against. It also removes the per-branch hard-coding from `TargetFrameworkDefaults.props`.

Drawbacks / open questions:

- Two repos on the same Arcade `main` but different SDKs would see different `NetCurrent` values. That is the point, but it is also a behavioural change.
- The exact "previous" and "minimum" mapping for each major version needs an authoritative source (a table in `TargetFrameworkDefaults.props`, keyed off the SDK major).
- Source-build (`DotNetBuildSourceOnly == true`) currently collapses `NetPrevious`/`NetMinimum` onto `NetCurrent`. That behaviour should be preserved.

#### Option B — Decouple from the SDK entirely (open)

Is there a useful definition of `NetCurrent` that is independent of both the Arcade branch and the consuming SDK? For example, "the latest GA .NET TFM at the time of build" derived from an external source. This is listed as an open question; it is not obvious that decoupling from the SDK actually adds value over Option A.

#### Interaction with `BundledNETCoreAppTargetFramework`

`ArcadeSdk.md` already directs most consumers to prefer `$(BundledNETCoreAppTargetFramework)` (defined by the .NET SDK itself) over `$(NetCurrent)`. That guidance should be reinforced; `NetCurrent` should be reserved for the small set of repos that genuinely need an Arcade-managed floating TFM.

### 4.3 Keep Arcade compatible with the SDKs that map to `$(NetMinimum)` and newer

Arcade `main` and everything inside `eng/common` must build and run on every .NET SDK from `$(NetMinimum)` up to the in-development one. Today that is .NET 10 and .NET 11.

The supported floor tracks `$(NetMinimum)`. When .NET 12 becomes current and is being worked on, `$(NetMinimum)` would still point at `net10.0`, so .NET 10 would remain supported; .NET 9 would not be in scope. The general rule: the SDK floor is whatever `$(NetMinimum)` resolves to.

Concretely, this means:

- Arcade project TFMs stay at `$(NetMinimum)` (already done).
- Targets, tasks, and scripts in `eng/common` are tested on the current and `$(NetMinimum)` SDKs in CI.
- Any new dependency added to Arcade or to `eng/common` must be compatible with the `$(NetMinimum)` SDK.

## 5. Benefits

- **Cross-cutting VMR changes become cheap.** Once Arcade `main` works on the current and previous major SDKs and TFM properties no longer change meaning per branch, a single change in Arcade `main` plus per-repo PRs can land everywhere — inner repos no longer need to also pick it up via Arcade `release/10.0` (or another older Arcade branch) for their standalone builds just because they're pinned to an older SDK.
- **Inner repos move at their own pace.** Each repo can pick its SDK update cadence (via Dependabot) independent of Arcade releases.
- **Clearer semantics for project authors.** `NetCurrent` means the same thing regardless of which Arcade branch is in play.

## 6. Risks and open questions

- **Source-build (`DotNetBuildSourceOnly`)**: The existing collapse of `NetPrevious`/`NetMinimum` onto `NetCurrent` when building from source must be preserved by whichever option is chosen in §4.2.
- **`NetCurrent` consumers that expected "in-development .NET"**: A small number of repos use `$(NetCurrent)` precisely to follow the in-development version. Under Option A, that behaviour requires those repos to keep their SDK on the preview channel themselves.
- **CI cost**: Validating Arcade and `eng/common` against N and N-1 SDKs increases CI matrix size. The cost should be scoped before committing.
- **Repos relying on PCS-driven SDK uplift for compliance**: Some teams may rely on Arcade pushing the SDK as their only signal to update. Dependabot adoption needs to be in place before turning off the PCS flow.
- **Coexistence period**: How long do both SDK-flow mechanisms coexist, and what's the off-ramp for repos still on PCS-driven SDK flow?

## 7. Migration plan

Phased rollout. Phase 1 is already complete.

1. ✅ **Downgrade Arcade project TFMs to `$(NetMinimum)`.** Merged on `main` ([dotnet/arcade@98d7ce0](https://github.com/dotnet/arcade/commit/98d7ce08e83c980d2bcc30bf3c846c8b3630c391)). Lets consumer repos on the previous-major SDK load Arcade's tools/tasks.
2. **Enable Dependabot SDK updates in consumer repos** (or opt out of PCS flow via `tools.pinned: true`). Roll this out repo by repo; verify Dependabot PRs reliably keep the SDK current before turning off PCS flow for that repo. Repos that prefer to pin can opt out immediately by adding `"tools": { "pinned": true }` to `global.json`.
3. **Teach `eng/common` to handle `sdk.version`-only `global.json`.** Update `tools.ps1`/`tools.sh`/`dotnet-install.*`/`InstallDotNetCore.*` so the absence of `tools.dotnet` falls back to `sdk.version`. This lets repos drop the redundant `tools.dotnet` entry and simplify their `global.json`.
4. **Redefine the floating TFM properties.** Implement Option A (or the chosen alternative) in `TargetFrameworkDefaults.props`. Validate against representative consumers, including a source-build run.
5. **Validate Arcade `main` against the previous major SDK.** Add a CI leg that builds and tests Arcade and `eng/common` on the N-1 SDK.
6. **Disable PCS-driven SDK flow from the Arcade channel.** Remove the SDK version from the set of artifacts PCS flows. Communicate the change in advance; preserve as opt-in for any remaining repos that need it.

## 8. Alternatives considered

- **Status quo.** Keep the current coupling. Rejected because the VMR cross-cutting cost is real and growing.
- **TFM decoupling only; keep PCS SDK flow.** Solves the per-branch hard-coding problem but leaves consumer repos forced onto the in-development SDK. Doesn't unblock cross-cutting VMR changes.
- **Stop PCS SDK flow only; leave TFM properties as-is.** Repos can pick their SDK, but `$(NetCurrent)` still means whatever the upstream Arcade branch says, which is confusing for repos pinned to an older SDK.
- **Per-major-version Arcade releases.** Maintain `Arcade.net10`, `Arcade.net11`, etc. branches that consumers pick from. Higher maintenance cost; doesn't help VMR cross-cutting changes (the whole point is that one change should reach all inner repos).

## References

- [`global.json`](../global.json) — current SDK pin
- [`eng/common/tools.ps1`](../eng/common/tools.ps1) / [`eng/common/tools.sh`](../eng/common/tools.sh) — bootstrap that currently requires `tools.dotnet`
- [`src/Microsoft.DotNet.Arcade.Sdk/tools/TargetFrameworkDefaults.props`](../src/Microsoft.DotNet.Arcade.Sdk/tools/TargetFrameworkDefaults.props) — current floating TFM definitions
- [`Documentation/ArcadeSdk.md`](./ArcadeSdk.md) §`NetCurrent/NetPrevious/NetMinimum/NetFrameworkMinimum`
- [Using Dependabot to manage .NET SDK updates (devblogs)](https://devblogs.microsoft.com/dotnet/using-dependabot-to-manage-dotnet-sdk-updates/)
- [NuGet.Client `global.json` — example of `sdk.version`-only (no `tools.dotnet`)](https://github.com/NuGet/NuGet.Client/blob/e69ab5e626d307d35f29c3250225a2e12305f038/global.json#L4)

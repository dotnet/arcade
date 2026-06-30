# Test runner consolidation on Microsoft.Testing.Platform

## Why

Arcade currently maintains five parallel test-runner code paths in
`src/Microsoft.DotNet.Arcade.Sdk/tools/` (`XUnit`, `XUnitV3`, `MSTest`,
`VSTest`, `Microsoft.Testing.Platform`) plus two parallel Helix work-item
generators in `src/Microsoft.DotNet.Helix/Sdk/` (`xunit-runner` /
`CreateXUnitWorkItems` and `mtp-runner` / `CreateMTPWorkItems`).

Most of that surface area predates [Microsoft.Testing.Platform][mtp] (MTP):

* xUnit v3 [defaults to MTP][xunitv3-mtp] and produces a self-hosting test
  executable when paired with `Microsoft.Testing.Platform.MSBuild`.
* MSTest 4.x [defaults to MTP][mstest-runner] via `MSTest.Sdk` /
  `MSTest` metapackage.
* NUnit, TUnit, and other frameworks ship MTP runners.
* `dotnet test` is now MTP-aware (`TestingPlatformDotnetTestSupport`) and in
  .NET 10 the [`dotnet test --use-testing-platform`][dotnet-test-mtp]
  experience is the official orchestrator for MTP-based projects.
* VSTest is in maintenance mode; the [testfx team has stated][vstest-status]
  that future investment is on MTP.

The legacy paths therefore exist purely as compatibility surface. Each one
adds property names that have to be documented and supported, complicates
the `Test` target dispatch, and ships duplicated logger / TRX wiring that
MTP gives us for free.

## Goal

Reduce the Arcade test-execution surface to:

* **One** runner targets file in the Arcade SDK that invokes MTP exes
  directly (already present:
  [`tools/Microsoft.Testing.Platform/Microsoft.Testing.Platform.targets`][mtp-targets]).
* **One** thin `dotnet test` wrapper for projects that have not yet
  migrated to MTP — used only when the project is not a Testing Platform
  application.
* **One** Helix work-item generator
  ([`CreateMTPWorkItems`][create-mtp]) replacing both the legacy
  `CreateXUnitWorkItems` flow and any framework-specific work-item
  generators.

Helix orchestration itself (`SendHelixJob`, queue selection, correlation
payloads, `EnableHelixJobMonitor`, XHarness for iOS/Android/WASM, test
retry, AzDO test-run lifecycle) is **out of scope** — those are Arcade's
genuine value-add and are independent of which runner runs the test.

## Plan

This is staged so each phase is independently revertable.

### Phase 1 — Signal direction (this PR)

* Add this document.
* Emit MSBuild warnings when a project opts into a path that we intend to
  remove:
  * Setting `UsingToolXUnit` (any value) — property is already documented
    as deprecated in [`DefaultVersions.props`][defaultversions] but emits
    no warning.
  * Setting `UseVSTestRunner=true`.
  * Including `XUnitProject` items in a Helix project (use `MTPProject`
    instead — see the
    [Helix Sdk readme](../src/Microsoft.DotNet.Helix/Sdk/Readme.md)).
* Delete the orphaned `xunit-reporter.py` Helix Python script which is
  already self-marked as deprecated and is no longer invoked from any
  Arcade target.

No behavior changes for repos that do not set the deprecated properties.

### Phase 2 — Flip defaults

* Default `UseMicrosoftTestingPlatformRunner=true` is already in place for
  xUnit v3 ([`XUnitV3.targets`][xunitv3-targets]). Audit downstream repos
  via arcade-validation to confirm there are no remaining consumers
  relying on the VSTest path.
* Default `EnableMSTestRunner=true` for MSTest projects, with an opt-out
  for repos that still depend on VSTest-specific loggers / runsettings
  features.
* Remove `Microsoft.NET.Test.Sdk` from the implicit reference set for MTP
  projects (already conditional on `IsTestingPlatformApplication != true`
  in [`Tests.props`][tests-props]; ensure that flag is reliably set for
  every MTP path).

### Phase 3 — Drop xUnit v2

* Delete `tools/XUnit/` (`XUnit.targets` and `XUnit.Runner.targets`).
  xUnit v2 is in maintenance upstream; v3 has been the Arcade
  recommendation since [#15671][pr-15671].
* Repos still on v2 can pin to an older Arcade SDK or set
  `TestRunnerName=XUnit` and import their own targets.

### Phase 4 — Collapse runner targets

* Replace `XUnitV3.Runner.targets`, `VSTest.targets`, and the runner
  portion of `MSTest.targets` with a single `RunTests` target that:
  * for MTP projects (`IsTestingPlatformApplication=true`), execs the
    self-hosting test app with MTP reporter flags; and
  * for non-MTP projects, invokes `dotnet test` with standard
    `--logger trx;...` arguments.
* Framework-specific `.targets` shrink to package-reference lists only.

### Phase 5 — Helix-side cleanup

* Delete `tools/xunit-runner/` (`XUnitRunner.props`, `XUnitRunner.targets`,
  `XUnitPublish.targets` — the publish helper has already been generalized
  to `_MTPPublishTargetsPath`) and `CreateXUnitWorkItems.cs`. `MTPProject`
  already covers xUnit v3, MSTest 4, NUnit, and TUnit.
* Drop the related `XUnitProject`, `XUnitPublishTargetFramework`,
  `XUnitRuntimeTargetFramework`, `XUnitRunnerVersion`, and
  `XUnitArguments` properties from the SDK and the
  [SendingJobsToHelix doc][send-helix-doc].

### Phase 6 — `TestArchitectures` loop

* `Tests.targets`' inner/outer loops over `TestArchitectures` ×
  `TargetFrameworks` exist to feed runner CLIs. Once `RunTests` is just
  `dotnet test`, the .NET SDK's per-TFM dispatch covers the TFM axis. The
  architecture axis (x86/x64 multiplexing of a single binary on .NET
  Framework) is Arcade-specific and stays — but the wrapper shrinks to
  roughly 20 lines.

## Estimated impact

| Area | Files removed | LoC removed | Public properties / items removed |
|---|---|---|---|
| Arcade SDK runner targets | XUnit v2 dir, `VSTest.targets`, two `*.Runner.targets` | ~400 | `UseVSTestRunner`, `UsingToolXUnit`, `TestRunnerName=XUnit\|VSTest` |
| Helix Sdk legacy xUnit | `xunit-runner/`, `CreateXUnitWorkItems.cs`, `xunit-reporter/` | ~600 | `XUnitProject`, `XUnitPublishTargetFramework`, `XUnitRuntimeTargetFramework`, `XUnitRunnerVersion`, `XUnitArguments`, `EnableXUnitReporter` |
| **Total** | **~10 files** | **~1000 LoC** | **8 properties + 1 item group** |

[mtp]: https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro
[mtp-targets]: ../src/Microsoft.DotNet.Arcade.Sdk/tools/Microsoft.Testing.Platform/Microsoft.Testing.Platform.targets
[create-mtp]: ../src/Microsoft.DotNet.Helix/Sdk/CreateMTPWorkItems.cs
[xunitv3-targets]: ../src/Microsoft.DotNet.Arcade.Sdk/tools/XUnitV3/XUnitV3.targets
[defaultversions]: ../src/Microsoft.DotNet.Arcade.Sdk/tools/DefaultVersions.props
[tests-props]: ../src/Microsoft.DotNet.Arcade.Sdk/tools/Tests.props
[send-helix-doc]: ./AzureDevOps/SendingJobsToHelix.md
[xunitv3-mtp]: https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
[mstest-runner]: https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-runner-intro
[dotnet-test-mtp]: https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-integration-dotnet-test
[vstest-status]: https://github.com/microsoft/vstest/blob/main/README.md
[pr-15671]: https://github.com/dotnet/arcade/pull/15671

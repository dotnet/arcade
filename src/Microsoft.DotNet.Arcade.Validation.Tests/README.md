# Microsoft.DotNet.Arcade.Validation.Tests

Validation tests for the Arcade SDK, ported from
[dotnet/arcade-validation](https://github.com/dotnet/arcade-validation) (`src/Validation/tests`).

These tests build small synthetic repositories using the Arcade SDK and assert on Arcade's
signing/build infrastructure behavior (for example, `AllowEmptySignList`, `UseDotNetCertificate`,
and the `ItemsToSign` error path). They exist so that the **arcade PR and official builds can
validate the newly produced Arcade SDK directly**, replacing the old
`arcade -> arcade-validation` promotion/validation gate (dotnet/arcade#17046).

## Validating the newly produced SDK

By default the synthetic repos use the Arcade/Helix SDK version pinned in the copied `global.json`
and restore from the public dnceng feeds. In CI we instead want to validate the SDK that the
current build just produced. The test host therefore reads these environment variables
(see `RepoResources.Create`):

| Variable | Purpose |
| --- | --- |
| `ARCADE_VALIDATION_SDK_VERSION` | Arcade/Helix SDK version injected into each synthetic repo's `global.json`. |
| `ARCADE_VALIDATION_DOTNET_VERSION` | Optional .NET SDK version override for the synthetic repo. |
| `ARCADE_VALIDATION_LOCAL_FEEDS` | `;`-separated local package feed directories (e.g. `artifacts/packages/<config>/Shipping` and `.../NonShipping`) injected with priority into each synthetic repo's `NuGet.config`. |

When these are unset, the tests fall back to the `global.json` versions and public feeds, which is
convenient for local development against an already-published SDK.

## Running

Use the helper script, which discovers the produced version and local feeds automatically:

```powershell
eng\run-arcade-validation-tests.ps1 -configuration Release
```

This project is intentionally **excluded** from the Helix unit-test submission in
`tests/UnitTests.proj`: the tests spawn full `build.ps1`/`build.sh` sub-builds and must run on a
build agent that has the freshly built packages available in a local feed. They are **not** run by
any pipeline yet — wiring them into the PR build (`azure-pipelines-pr.yml`) and the official build's
`ValidateSdk` stage (`eng/validate-sdk.yml`) follows in a subsequent increment.

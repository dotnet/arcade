# Authenticating OneLocBuild's GitHub check-in with the GitHub App

This document explains how a repository gains access to, and opts in to, the **GitHub App**
authentication path for the OneLocBuild localization check-in PR. It supplements the main
[OneLocBuild in Arcade](OneLocBuild.md) documentation.

## Background: why this change

When OneLocBuild is configured for a GitHub-based repo (`RepoType: gitHub`), the task opens (or
updates) a pull request into the repo to check in the localized files. Historically that PR was
authenticated with a shared, long-lived classic PAT (`BotAccount-dotnet-bot-repo-PAT`, from the
`OneLocBuildVariables` variable group).

The **Microsoft Open Source** enterprise policy forbids classic PATs with a lifetime longer than a
few days, which the shared PAT violates. To comply, the OneLocBuild job template can instead mint a
**short-lived GitHub App installation token** (`ghs_…`) at build time and use it for the check-in
PR. Installation tokens are exempt from the classic-PAT lifetime policy, so they are a durable
replacement.

The GitHub App used for this is **`dotnet OneLoc Localization`** (owned by `@dotnet-bot`). Its only
job is to open/update the localization check-in PR on your repository.

## How it works (opt-in, backward-compatible)

The App path is **opt-in** and does not change behavior for any repo that doesn't configure it. In
[`onelocbuild.yml`](/eng/common/core-templates/job/onelocbuild.yml), the App token is minted only
when **all** of the following are true:

- `GitHubAppServiceConnection` is set to a non-empty value, **and**
- `RepoType` is `gitHub`, **and**
- the build is running in the **`internal`** Azure DevOps project (the App service connection and
  Key Vault key are scoped to `dnceng/internal`).

When those hold, the job runs [`get-github-app-token.yml`](/eng/common/templates/steps/get-github-app-token.yml),
which signs a JWT with the App's RSA key in Key Vault, exchanges it for an installation token, and
passes that token to the OneLocBuild task via `gitHubPatVariable` **instead of** the shared PAT.

If `GitHubAppServiceConnection` is left at its default (`''`) — or the build runs in any project
other than `internal` (e.g. `DevDiv`, `public`) — the job falls back to the existing PAT-based
authentication. **No action is required for repos that want to keep using the PAT.**

## Gaining access

"Access" means two separate things, and **both** are required:

1. **The App must be installed on the GitHub org/account that owns your target repo, and your
   specific repository must be selected in that installation.** The App can only open a PR against a
   repository it is installed on. This is what actually grants the App permission to your repo.
2. **Your pipeline must opt in** by passing the GitHub App parameters to the `onelocbuild.yml`
   template (see below).

### Step 1 — Request that your repository be added to the App installation

The App installation and the backing `dnceng/internal` service connection / Key Vault key are
managed by the .NET Engineering Services (dnceng) team. To have your repo added:

1. Identify the **GitHub org** and **repository** your OneLoc check-in PR targets. For most repos
   this is the value of the `GitHubOrg` parameter (default `dotnet`) and your repo name. If you use
   a mirrored repository, it's the `GitHubOrg`/`MirrorRepo` the PR is opened against — **not** the
   Azure DevOps mirror.
2. Reach out to the **First Responders**
   [channel](https://teams.microsoft.com/l/channel/19%3Aafba3d1545dd45d7b79f34c1821f6055%40thread.skype/First%20Responders?groupId=4d73664c-9f2f-450d-82a5-c2f02756606d&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47)
   and ask them to add your repository to the **`dotnet OneLoc Localization`** GitHub App
   installation for the appropriate org.
3. The App must have permission to open pull requests (Contents + Pull requests: read & write) on
   the selected repository. dnceng configures this as part of the installation.

> **Note:** The App is installed per GitHub organization. If your repo lives in an org where the App
> is not yet installed, dnceng will need to install and approve it in that org first, which may
> require an org owner's approval.

### Step 2 — Opt your pipeline in

Once your repo is part of the App installation, add the GitHub App parameters to your OneLocBuild
template call. For example:

```yaml
- ${{ if eq(variables['Build.SourceBranch'], 'refs/heads/main') }}:
  - template: /eng/common/templates/job/onelocbuild.yml
    parameters:
      LclSource: lclFilesfromPackage
      LclPackageId: 'LCL-JUNO-PROD-YOURREPO'
      # Opt in to GitHub App authentication for the check-in PR:
      GitHubAppServiceConnection: 'dnceng-oneloc-githubapp'
      GitHubAppClientId: 'Iv23lijBU8x3gc9lDOc9'
      GitHubAppKeyVaultName: 'EngKeyVault'
      GitHubAppKeyName: 'oneloc-localization-app-key'
```

These values are non-secret identifiers for the dnceng-managed `dotnet OneLoc Localization` App and
its Key Vault signing key. Confirm the current values with the First Responders when you onboard, in
case they change.

### GitHub App parameters

| **Parameter** | **Default** | **Notes** |
|:-:|:-:|-|
| `GitHubAppServiceConnection` | `''` | The Azure DevOps **WIF service connection** (in `dnceng/internal`) whose identity has `Sign` permission on the App's Key Vault key. Setting this to a non-empty value is what activates the App path. Leave empty to keep using the PAT. |
| `GitHubAppClientId` | `''` | The GitHub App's **Client ID** (used as the JWT `iss` claim). |
| `GitHubAppKeyVaultName` | `''` | The Key Vault holding the App's RSA signing key (e.g. `EngKeyVault`). |
| `GitHubAppKeyName` | `''` | The name of the RSA key inside that Key Vault (the App's private key). |

The token is minted for the installation on the `GitHubOrg` account (default `dotnet`), so make sure
`GitHubOrg` (and `MirrorRepo`, if mirroring) point at the org/repo where the App is installed.

## Verifying it works

1. Run your pipeline (on the `internal` project) from a branch where the OneLocBuild job runs.
2. In the build, confirm the **`Get GitHub App installation token`** step runs and succeeds before
   the `OneLocBuild` task.
3. Confirm the check-in PR is opened by the **`dotnet OneLoc Localization`** App (the PR author will
   be the App / its bot identity) rather than by `dotnet-bot` via the shared PAT.

## Troubleshooting

- **The App-token step is skipped.** The App path only activates when `GitHubAppServiceConnection`
  is non-empty, `RepoType` is `gitHub`, and the build runs in the `internal` project. Verify all
  three. Builds in `public`/`DevDiv` intentionally fall back to the PAT.
- **Token minting fails with a Key Vault authorization error.** The service connection identity
  needs the `Key Vault Crypto User` role (or at least the `Sign` action) on the App's key. Contact
  First Responders.
- **`404`/`Not Found` when requesting the installation token.** The App is not installed on the
  `GitHubOrg` account, or your repository was not selected in the installation. Complete Step 1.
- **PR fails to open on your repo.** Ensure the App has `Contents` and `Pull requests` (read &
  write) permission on the selected repository, and that your repo is included in the installation.

## Scope and limitations

- The App path is only available in the **`dnceng/internal`** Azure DevOps project. Pipelines in
  other projects (e.g. DevDiv-hosted loc pipelines) keep using PAT-based auth and are not covered by
  the `dnceng-oneloc-githubapp` service connection.
- Opting in is **not required** to keep localization working — the default PAT path continues to
  function. This App is the compliant, long-term replacement, and repos are encouraged to migrate.

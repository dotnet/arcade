---
name: "Build Failure Analysis (command)"
description: >-
  Rerun the build-failure analysis on a pull request when a maintainer comments
  `/analyze-build-failure`. Same body as `build-failure-analysis.md` — it does
  NOT rebuild: it finds the PR's most recent **failed** Azure Pipelines
  `arcade-pr` build, downloads the binary log that build already produced, and
  delegates to the `build-failure-analyst` agent (which queries the binlog live
  via the containerized `binlog-mcp` MCP server). Useful when a previous run
  was cancelled, the analysis comment was dismissed, or the agent needs another
  pass after a force-push.

on:
  slash_command:
    name: analyze-build-failure
    events: [pull_request_comment]
    strategy: centralized
  roles: [admin, maintainer, write]
  reaction: "eyes"
  # Gate the AI pipeline on the fetch job so the agent only runs when a binlog
  # was actually retrieved from a failed Azure DevOps build.
  needs: [fetch-binlog]

# Skip activation (and the agent) unless a binlog was retrieved — e.g. if the
# PR's latest Azure DevOps build did not fail, there is nothing to analyse.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  group: build-failure-analysis-${{ github.event.issue.number || github.event.pull_request.number || fromJSON(github.event.inputs.aw_context || github.event.client_payload.aw_context || '{}').item_number || github.run_id }}
  cancel-in-progress: true

env:
  NUGET_MCP_VERSION: '1.4.3'

timeout-minutes: 30

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Live binlog access for the agent — see build-failure-analysis.md for the
# rationale. The fetch-binlog job downloads the binlog from Azure DevOps and
# uploads it as an artifact; the agent job downloads it to `/tmp/build.binlog`
# and the gh-aw MCP gateway mounts it read-only at `/data/build.binlog`.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/build.binlog:/data/build.binlog:ro"
    allowed: ["*"]

# Custom job that reuses the binlog from the PR's most recent failed Azure
# DevOps `arcade-pr` build instead of rebuilding. Mirrors the fetch-binlog job
# in build-failure-analysis.md; the only difference is that it locates the
# build by the PR's merge branch (no `check_run` payload is available on a
# slash command).
jobs:
  fetch-binlog:
    name: Fetch binlog (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    permissions:
      contents: read
      pull-requests: read
    outputs:
      binlog-found: ${{ steps.fetch.outputs.binlog-found }}
      pr-number: ${{ steps.fetch.outputs.pr-number }}
      pr-head-sha: ${{ steps.fetch.outputs.pr-head-sha }}
      ado-build-id: ${{ steps.fetch.outputs.ado-build-id }}
      ado-build-url: ${{ steps.fetch.outputs.ado-build-url }}
    steps:
      - name: Download binlog from the PR's latest failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # arcade-pr pipeline definition id in dnceng-public/public.
          ADO_BUILD_DEFINITION_ID: "283"
          PR_NUMBER: ${{ github.event.issue.number }}
        run: |
          # Relax errexit/pipefail: advisory + best-effort. On any gap we emit
          # binlog-found=false and the agent pipeline stays inert.
          set +e
          set +o pipefail

          if [ -z "${PR_NUMBER}" ]; then
            echo "::warning::No PR number on the slash-command event; nothing to analyze."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi

          # --- 1. Find the PR's most recent failed arcade-pr build ---
          # Arcade builds the PR merge ref (refs/pull/<n>/merge).
          builds_json=$(curl -sSL --retry 3 \
            "${ADO_API}/build/builds?definitions=${ADO_BUILD_DEFINITION_ID}&branchName=refs/pull/${PR_NUMBER}/merge&statusFilter=completed&resultFilter=failed&queryOrder=finishTimeDescending&\$top=1&api-version=7.1")
          BUILD_ID=$(printf '%s' "${builds_json}" | jq -r '.value // [] | .[0].id // empty')
          echo "Latest failed arcade-pr build for PR #${PR_NUMBER}: '${BUILD_ID}'"
          if [ -z "${BUILD_ID}" ]; then
            echo "::warning::No completed+failed arcade-pr build found for PR #${PR_NUMBER}; nothing to analyze."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi

          HEAD_SHA=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" --jq '.head.sha' 2>/dev/null)

          # --- 2. Find a Logs_Build_* artifact (prefer Linux_Debug) ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          DL=$(printf '%s' "${artifacts_json}" \
            | jq -r '.value // [] | map(select(.name | test("^Logs_Build_")))
                     | (map(select(.name | test("Linux_Debug"))) + .)
                     | .[0].resource.downloadUrl // empty')
          if [ -z "${DL}" ]; then
            echo "::warning::No Logs_Build_* artifact found on Azure DevOps build ${BUILD_ID}."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi

          # --- 3. Download + extract the newest *.binlog ---
          curl -sSL --retry 3 "${DL}" -o /tmp/logs.zip
          mkdir -p /tmp/logs
          unzip -q /tmp/logs.zip -d /tmp/logs
          BINLOG=$(find /tmp/logs -name '*.binlog' -type f -printf '%T@ %p\n' 2>/dev/null \
            | sort -rn | head -1 | cut -d' ' -f2-)
          if [ -z "${BINLOG}" ] || [ ! -f "${BINLOG}" ]; then
            echo "::warning::No *.binlog inside the logs artifact of build ${BUILD_ID}."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi
          cp "${BINLOG}" /tmp/build.binlog
          echo "Staged binlog: ${BINLOG} -> /tmp/build.binlog"

          {
            echo "binlog-found=true"
            echo "pr-number=${PR_NUMBER}"
            echo "pr-head-sha=${HEAD_SHA}"
            echo "ado-build-id=${BUILD_ID}"
            echo "ado-build-url=${ADO_BUILD_UI}?buildId=${BUILD_ID}"
          } >> "$GITHUB_OUTPUT"

      - name: Upload analysis artifact
        if: steps.fetch.outputs.binlog-found == 'true'
        uses: actions/upload-artifact@v7.0.1
        with:
          name: build-failure-analysis-data
          path: /tmp/build.binlog
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. The top-level `if:` gates these on a binlog
# having been retrieved, so the agent never runs without something to analyse.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8.0.1
    with:
      name: build-failure-analysis-data
      path: /tmp/

  - name: Setup .NET (for NuGet MCP Server)
    uses: actions/setup-dotnet@v5.4.0
    with:
      dotnet-version: '9.0.x'

  - name: Install NuGet MCP Server
    continue-on-error: true
    # See build-failure-analysis.md for why we install into a `bin` directory
    # under the runner tool cache (agent sandbox PATH) rather than `--global`,
    # and run from `/tmp` (avoid the repo's internal-SDK `global.json`).
    working-directory: /tmp
    run: |
      TOOL_DIR="${RUNNER_TOOL_CACHE:-/opt/hostedtoolcache}/nuget-mcp-server/bin"
      dotnet tool install NuGet.Mcp.Server --version "$NUGET_MCP_VERSION" --tool-path "$TOOL_DIR"
      echo "$TOOL_DIR" >> "$GITHUB_PATH"

  - name: Export agent context
    env:
      GH_AW_BINLOG_FOUND_VALUE: ${{ needs.fetch-binlog.outputs.binlog-found }}
      GH_AW_PR_NUMBER_VALUE: ${{ needs.fetch-binlog.outputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-head-sha }}
      GH_AW_ADO_BUILD_URL_VALUE: ${{ needs.fetch-binlog.outputs.ado-build-url }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # See build-failure-analysis.md for the binlog path conventions. The
      # binlog is read through the binlog-mcp MCP server (mounted at
      # `/data/build.binlog`); GH_AW_BINLOG_HOST_PATH points at the Azure
      # DevOps build for human-facing references.
      BINLOG_MCP_PATH=""
      if [ "${GH_AW_BINLOG_FOUND_VALUE:-false}" = "true" ] && [ -f /tmp/build.binlog ]; then
        BINLOG_MCP_PATH="/data/build.binlog"
      fi
      {
        echo "GH_AW_BUILD_OUTCOME=failure"
        echo "GH_AW_BINLOG_PATH=${BINLOG_MCP_PATH}"
        echo "GH_AW_BINLOG_HOST_PATH=${GH_AW_ADO_BUILD_URL_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
      } >> "$GITHUB_ENV"

tools:
  github:
    toolsets: [pull_requests, repos]
  bash:
    - "cat"
    - "head"
    - "tail"
    - "grep"
    - "wc"
    - "sort"
    - "uniq"
    - "ls"
    - "find"
    - "dotnet"
    - "NuGet.Mcp.Server"

safe-outputs:
  messages:
    footer: "> 🤖 **Automated content by GitHub Copilot.** Generated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} · [◷]({history_link})"
  # The agent targets the resolved PR via `GH_AW_PR_NUMBER` (`target: "*"`),
  # matching the auto-trigger workflow. Caps absorb Copilot CLI retry
  # amplification; report-as-issue is disabled so flakes don't spam issues.
  report-failure-as-issue: false
  add-comment:
    max: 5
    target: "*"
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 25
    target: "*"
  noop:
    max: 5
    report-as-issue: false
---

<!--
  Body provided by shared/build-failure-analysis-shared.md.
-->

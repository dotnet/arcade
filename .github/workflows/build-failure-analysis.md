---
name: "Build Failure Analysis"
description: >-
  When the Azure Pipelines PR build (`arcade-pr`) fails, downloads the binary
  log that build already produced — it does NOT rebuild — and delegates to the
  `build-failure-analyst` agent, which queries the binlog live via the
  containerized `binlog-mcp` MCP server to identify root causes, post a PR
  comment summarizing them, and attach inline `suggestion` blocks tied to the
  diff.

# This workflow is **advisory**, not gating, and it performs **no build of its
# own**. Arcade's authoritative PR build runs on Azure DevOps
# (dnceng-public/public, pipeline "arcade-pr", definitionId 283) and publishes
# its binary logs as `Logs_Build_*` pipeline artifacts. When that build's
# GitHub check reports failure, this workflow downloads the already-produced
# binlog (anonymously — dnceng-public/public is a public project) and analyses
# it. Reusing the binlog avoids a duplicate ~build and also removes any
# fork-PR code-execution risk: this workflow only downloads an artifact
# (data), it never checks out or runs PR code.

on:
  # `check_run` fires for every check on a commit, so the `fetch-binlog` job
  # below filters tightly to the `arcade-pr` build check reporting failure.
  check_run:
    types: [completed]
  # Manual entry point for reruns / testing: analyse a specific Azure DevOps
  # build id and post to a specific PR.
  workflow_dispatch:
    inputs:
      ado-build-id:
        description: "Azure DevOps build id to analyze (dnceng-public/public)."
        required: true
        type: string
      pr-number:
        description: "PR number to post the analysis on."
        required: true
        type: string
  # Gate the whole AI pipeline on the fetch job so the agent only runs when a
  # binlog was actually retrieved.
  needs: [fetch-binlog]

# Activate (and therefore run the agent) only when the fetch job produced a
# binlog. When `check_run` fires for an unrelated / passing check the
# fetch-binlog job is skipped, its output is empty, and this cascades into a
# skipped agent — no AI calls on anything but a real `arcade-pr` failure.
if: needs.fetch-binlog.outputs.binlog-found == 'true'

permissions:
  contents: read
  pull-requests: read
  copilot-requests: write

concurrency:
  group: build-failure-analysis-${{ github.event.check_run.pull_requests[0].number || inputs.pr-number || github.event.check_run.head_sha || github.run_id }}
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

# Live binlog access for the agent. The binlog is downloaded from Azure
# DevOps by the fetch-binlog job, uploaded as an artifact, downloaded by the
# agent job to `/tmp/build.binlog`, and mounted read-only into this container
# at `/data/build.binlog` by the gh-aw MCP gateway.
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/build.binlog:/data/build.binlog:ro"
    allowed: ["*"]

# Custom job that reuses the binlog from the failed Azure DevOps build instead
# of rebuilding. It resolves the ADO build id from the check's details URL
# (or the dispatch input), downloads the published `Logs_Build_*` artifact,
# extracts the newest `*.binlog`, and uploads it for the agent job. The agent
# pipeline only runs when this job reports `binlog-found == 'true'`.
jobs:
  fetch-binlog:
    name: Fetch binlog (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # `check_run` fires for every check; only act on the arcade PR build check
    # reporting failure (or a manual dispatch). This keeps the AI pipeline off
    # unrelated / passing checks.
    if: >
      github.event_name == 'workflow_dispatch' ||
      (github.event.check_run.name == 'arcade-pr' && github.event.check_run.conclusion == 'failure')
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
      - name: Download binlog from the failed Azure Pipelines build
        id: fetch
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          # Azure DevOps public org/project + arcade PR pipeline.
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          EVENT_NAME: ${{ github.event_name }}
          CHECK_DETAILS_URL: ${{ github.event.check_run.details_url }}
          CHECK_HEAD_SHA: ${{ github.event.check_run.head_sha }}
          CHECK_PR_NUMBER: ${{ github.event.check_run.pull_requests[0].number }}
          DISPATCH_BUILD_ID: ${{ inputs.ado-build-id }}
          DISPATCH_PR_NUMBER: ${{ inputs.pr-number }}
        run: |
          # Relax errexit/pipefail: this is advisory and best-effort — on any
          # gap we emit binlog-found=false and the agent pipeline stays inert.
          set +e
          set +o pipefail

          # --- 1. Resolve the Azure DevOps build id ---
          BUILD_ID=""
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            BUILD_ID="${DISPATCH_BUILD_ID}"
          else
            # details_url looks like: .../_build/results?buildId=NNN&view=...
            BUILD_ID=$(printf '%s' "${CHECK_DETAILS_URL}" | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
          fi
          echo "Resolved Azure DevOps build id: '${BUILD_ID}'"
          if [ -z "${BUILD_ID}" ]; then
            echo "::warning::Could not resolve an Azure DevOps build id from '${CHECK_DETAILS_URL}'; nothing to analyze."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi

          # --- 2. Resolve the PR number and head SHA to comment on ---
          PR_NUMBER=""
          HEAD_SHA=""
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            PR_NUMBER="${DISPATCH_PR_NUMBER}"
          else
            PR_NUMBER="${CHECK_PR_NUMBER}"
            HEAD_SHA="${CHECK_HEAD_SHA}"
          fi
          # Fork PRs don't populate check_run.pull_requests, so fall back to the
          # commit -> PR association API.
          if [ -z "${PR_NUMBER}" ] && [ -n "${HEAD_SHA}" ]; then
            PR_NUMBER=$(gh api "repos/${GH_AW_REPO}/commits/${HEAD_SHA}/pulls" --jq '.[0].number' 2>/dev/null)
          fi
          if [ -z "${HEAD_SHA}" ] && [ -n "${PR_NUMBER}" ]; then
            HEAD_SHA=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" --jq '.head.sha' 2>/dev/null)
          fi
          echo "PR number: '${PR_NUMBER}', head sha: '${HEAD_SHA}'"
          if [ -z "${PR_NUMBER}" ]; then
            echo "::warning::Could not resolve a PR number for build ${BUILD_ID}; nothing to post to."
            echo "binlog-found=false" >> "$GITHUB_OUTPUT"
            exit 0
          fi

          # --- 3. Find a Logs_Build_* artifact (prefer Linux_Debug) ---
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

          # --- 4. Download + extract the newest *.binlog ---
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

# Steps that run in the agent job. Because the top-level `if:` gates activation
# on `needs.fetch-binlog.outputs.binlog-found == 'true'`, these only run once a
# binlog has been retrieved from the failed Azure DevOps build.
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
    # Run from `/tmp` so `dotnet` does not walk into the repo's `global.json`
    # (which pins an internal-only SDK preview). Install into a `bin` directory
    # under the runner tool cache instead of `--global`: the gh-aw/AWF sandbox
    # that runs the agent's shell tools builds its PATH from `bin` directories
    # found under the tool cache and does NOT mount ~/.dotnet/tools, so
    # `--global` would leave `NuGet.Mcp.Server` uninvokable by the agent.
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
      # The binlog is mounted into the binlog-mcp container at
      # `/data/build.binlog`; the agent passes that path as `binlog_file` on
      # every `binlog_*` MCP call. `GH_AW_BINLOG_HOST_PATH` points at the
      # originating Azure DevOps build for human-facing references.
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
  # `check_run` carries no native issue/PR context for gh-aw, so the agent must
  # target the resolved PR explicitly (`target: "*"`) using `GH_AW_PR_NUMBER`.
  # Caps absorb Copilot CLI retry amplification; report-as-issue is disabled so
  # transient flakes never spam tracking issues.
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

  All build-failure analysis expertise (binlog parsing, error grouping,
  suggestion authoring) lives in the reusable agent at
  .github/agents/build-failure-analyst.agent.md.
-->

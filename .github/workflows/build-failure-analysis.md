---
name: "Build Failure Analysis"
description: >-
  When the Azure Pipelines PR build (`arcade-pr`) fails, downloads the binary
  logs that build already produced — it does NOT rebuild — and delegates to
  the `build-failure-analyst` agent, which queries the binlogs live via the
  containerized `binlog-mcp` MCP server to identify root causes, post a PR
  comment summarizing them, and attach inline `suggestion` blocks tied to the
  diff.

# This workflow is **advisory**, not gating, and it performs **no build of its
# own**. Arcade's authoritative PR build runs on Azure DevOps
# (dnceng-public/public, pipeline "arcade-pr", definitionId 283) and publishes
# each build leg's binary log as a `Logs_Build_<leg>` pipeline artifact. When
# that build's GitHub check reports failure, this workflow downloads the
# binlogs from **all** build legs (anonymously — dnceng-public/public is a
# public project) and the agent analyses whichever leg(s) actually contain
# errors. Reusing the binlogs avoids a duplicate build: the analysis pipeline
# only downloads build artifacts (data) and reads them — it does **not** build
# or execute PR code. (gh-aw's generated agent job **does** check out the
# repository — via `actions/checkout` — to load the workflow's own agent
# configuration and, since the `checkout:` block below, the analysed PR head so
# the agent can author a fix commit. The PR tree is only read and edited as
# text; nothing in it is built or executed.)

on:
  # `check_run` fires for every check on a commit, so the `fetch-binlog` job
  # below filters tightly to the `arcade-pr` build check reporting failure.
  check_run:
    types: [completed]
  # Advisory analysis should run for **every** failing PR — including external
  # contributors' PRs, which are the most likely to break the build. Disable
  # gh-aw's default author-association gate (which would otherwise skip
  # non-write-access actors, and on `check_run` the actor is the pipeline app
  # anyway). This is safe here: the workflow only reads a public binlog and
  # posts advisory comments — it never builds or executes PR code. The one
  # write path that touches code (`push-to-pull-request-branch`) is refused
  # outright by gh-aw's handler for fork PRs, so `roles: all` cannot turn an
  # external contribution into a push.
  roles: all
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

# Activate (and run the agent) only when the fetch job retrieved at least one
# binlog. When `check_run` fires for an unrelated / passing check the
# fetch-binlog job is skipped, its output is empty, and this cascades into a
# skipped agent — no AI calls on anything but a real `arcade-pr` failure whose
# PR targets an in-scope base branch.
#
# `push-blocked` is the loop guard for the push escape hatch (fetch job, step
# 3c): when the branch tip is already an automated `[build-failure-analysis]`
# fix and the build still fails, the previous attempt did not converge and the
# pull request belongs to a human. Enforcing it here rather than inside the
# agent is deliberate — this condition skips the activation and agent jobs, and
# gh-aw's `safe_outputs` job is itself conditioned on the agent not being
# skipped, so there is no code path left that could push. Nothing the model
# does (or that a prompt injection makes it do) can re-enable it.
if: needs.fetch-binlog.outputs.binlog-found == 'true' && needs.fetch-binlog.outputs.push-blocked != 'true'

# Least-privilege for the workflow/agent jobs. The agent runs read-only; it
# does NOT post directly. All PR writes (summary comment + inline review
# suggestions + the fix commit) go through gh-aw **safe-outputs**, which the
# compiler emits as a separate `safe_outputs` job granted `pull-requests:
# write` + `issues: write` (and, for `push-to-pull-request-branch`, `contents:
# write`) in the generated lock. Keep `pull-requests: read` here so the AI
# agent job stays least-privilege — do NOT raise it to `write`, that would
# hand PR-write scope to the agent job unnecessarily.
#
# Do NOT add `copilot-requests: write` here. That permission switches gh-aw's
# generated lock from `COPILOT_GITHUB_TOKEN: ${{ secrets.COPILOT_GITHUB_TOKEN }}`
# to `${{ github.token }}`, and the ephemeral Actions token is not entitled for
# inference against api.githubcopilot.com in this org — every agent run then
# dies in ~2s with "Authentication failed with provider ... (HTTP 403)" on both
# /models and /chat/completions, before it reads the prompt or opens a binlog.
# `update-default-versions.md` omits it and works; keep this consistent.
permissions:
  contents: read
  pull-requests: read

concurrency:
  # Only real `arcade-pr` check_run events (and manual dispatch for a PR) use a
  # PR/head-scoped group, so a newer analysis supersedes an in-progress one for
  # the same PR. Every OTHER completed check_run on the PR would otherwise land
  # in the same group and — with cancel-in-progress — abort the running real
  # analysis, so those get a unique per-run group that collides with nothing.
  group: ${{ (github.event_name == 'check_run' && github.event.check_run.name == 'arcade-pr' && format('build-failure-analysis-{0}', github.event.check_run.pull_requests[0].number || github.event.check_run.head_sha)) || (github.event_name == 'workflow_dispatch' && format('build-failure-analysis-{0}', inputs['pr-number'])) || format('build-failure-analysis-run-{0}', github.run_id) }}
  cancel-in-progress: true

timeout-minutes: 30

# The agent job's default checkout uses the event ref, and for `check_run` that
# is the repository's DEFAULT BRANCH — not the pull request. Without this block
# the workspace holds `main`, so an agent asked to fix a PR file would patch the
# wrong revision: `push-to-pull-request-branch` pushes the *file contents* of
# the agent's tree onto the PR branch, so a fix authored against `main` would
# silently revert anything else that changed in that file. `pr-checkout-ref` is
# the PR's head branch for same-repo PRs (attached, so gh-aw can derive the push
# target from `git rev-parse --abbrev-ref HEAD`) and `refs/pull/<n>/head` for
# forks, which gh-aw refuses to push to anyway.
#
# Checking out the PR head does NOT execute PR code: this workflow never builds,
# and the agent's bash allowlist contains no interpreters, package managers or
# build tools — the tree is read and edited as text only.
#
# The PR-head checkout is intentionally shallow (`actions/checkout`'s default
# depth of 1): gh-aw bundles only the commits the agent creates on top of it, so
# no history is needed. Step 6b's loop guard therefore reads the PR's commit
# list through the GitHub tools rather than `git log`, which cannot see the
# branch's history here.
#
# It does, however, put PR-controlled `.github/`, `.agents/`, `AGENTS.md` and
# every other agent-config path in the workspace, and the agent reads its
# playbook from there. gh-aw's
# own base-branch restore (`restore_base_github_folders.sh`) is gated on its
# built-in PR-checkout step, which never fires for `check_run` (that event
# carries no `pull_request` payload), so the second checkout below fetches the
# same agent config from the base branch and a `pre-agent-steps` step copies it
# over the PR's copy before the agent starts. Without that, a fork PR could
# rewrite the analyst's own instructions — `roles: all` lets every fork reach
# this workflow.
checkout:
  - ref: ${{ needs.fetch-binlog.outputs.pr-checkout-ref }}
  # The pull request's own base branch (resolved from the GitHub API by the
  # fetch job and validated to be `main` or `release/*`), not the repository
  # default branch: a `release/*` pull request must be analyzed with the
  # playbook and agent config that branch actually carries, otherwise the
  # restore below would silently swap in `main`'s instructions. Falls back to
  # the default branch if the API lookup returned nothing.
  - ref: ${{ needs.fetch-binlog.outputs.base-ref || github.event.repository.default_branch }}
    path: .gh-aw-base-config
    fetch-depth: 1
    # Cone mode (the `actions/checkout` default) materializes every top-level
    # file in addition to the listed directories. The restore step below does
    # not rely on that: it consults the base tree directly, so a sparse-checkout
    # change cannot turn "restore" into "delete". The directory list mirrors
    # gh-aw's own `GH_AW_AGENT_FOLDERS` (see the generated lock) — every path
    # the engine treats as agent configuration, not just the ones this repo
    # happens to use today, so a PR cannot introduce e.g. `.claude/` and have it
    # survive into the agent's context.
    sparse-checkout: |
      .agents
      .antigravity
      .claude
      .codex
      .crush
      .gemini
      .github
      .opencode
      .pi

pre-agent-steps:
  - name: Restore agent config from the base branch
    shell: bash
    env:
      BASE_BRANCH: ${{ needs.fetch-binlog.outputs.base-ref || github.event.repository.default_branch }}
    run: |
      set -euo pipefail
      BASE=".gh-aw-base-config"
      # Mirror gh-aw's restore_base_github_folders.sh: for each agent-config
      # path, prefer the base-branch copy, and delete anything the PR added that
      # the base branch does not have. The two lists below are gh-aw's own
      # `GH_AW_AGENT_FOLDERS`/`GH_AW_AGENT_FILES` (see the generated lock), with
      # `.mcp.json` added because this engine also auto-loads it — keeping them
      # in sync means the mitigation covers every path the engine recognizes,
      # not only the ones this repo uses. Unknown paths simply do not exist and
      # cost nothing.
      for FOLDER in .agents .antigravity .claude .codex .crush .gemini .github .opencode .pi; do
        rm -rf "${FOLDER}"
        if [ -d "${BASE}/${FOLDER}" ]; then
          cp -r "${BASE}/${FOLDER}" "${FOLDER}"
          echo "Restored ${FOLDER} from ${BASE_BRANCH}"
        else
          echo "Base branch has no ${FOLDER}; removed the PR's copy"
        fi
      done
      BASE_ROOT_FILES=$(git -C "${BASE}" ls-tree --name-only HEAD)
      for FILE in .crush.json .mcp.json AGENTS.md ANTIGRAVITY.md CLAUDE.md GEMINI.md PI.md opencode.jsonc; do
        rm -f "${FILE}"
        if [ -f "${BASE}/${FILE}" ]; then
          cp "${BASE}/${FILE}" "${FILE}"
          echo "Restored ${FILE} from ${BASE_BRANCH}"
        elif printf '%s\n' "${BASE_ROOT_FILES}" | grep -qx -- "${FILE}"; then
          # On the base branch but not materialized by the sparse checkout.
          git -C "${BASE}" show "HEAD:${FILE}" > "${FILE}"
          echo "Restored ${FILE} from ${BASE_BRANCH} (via git show)"
        else
          # Genuinely absent on the base branch, so the PR added it: removing
          # it is the intended outcome.
          echo "Base branch has no ${FILE}; removed the PR's copy"
        fi
      done
      rm -rf "${BASE}"
      # gh-aw restores inline sub-agents/skills from the activation artifact in
      # the steps just above; the wipe above would drop them, so replay those
      # restores. They no-op when the workflow defines none (this one does not),
      # and are skipped entirely if a compiler upgrade renames the scripts.
      for SCRIPT in restore_inline_sub_agents.sh restore_inline_skills.sh; do
        if [ -f "${RUNNER_TEMP}/gh-aw/actions/${SCRIPT}" ]; then
          GH_AW_SUB_AGENT_DIR=".github/agents" \
          GH_AW_SUB_AGENT_EXT=".agent.md" \
          GH_AW_SKILL_DIR=".github/skills" \
            bash "${RUNNER_TEMP}/gh-aw/actions/${SCRIPT}"
        fi
      done
      # The restored files differ from the PR head, so leave them staged-free and
      # let git see them as modifications: the agent only ever commits the single
      # source file it fixes, and gh-aw builds its patch from commits, never from
      # the dirty worktree. The listing below is diagnostic only — it makes the
      # restored set visible in the job log when a push has to be explained
      # after the fact, and deliberately never fails the run.
      git -c core.fileMode=false status --porcelain -- .github .agents AGENTS.md | head -n 20 || true

network:
  allowed:
    - defaults
    - dotnet

imports:
  - shared/build-failure-analysis-shared.md

# Live binlog access for the agent. The build-leg binlogs are downloaded from
# Azure DevOps by the fetch-binlog job into a directory, uploaded as an
# artifact, downloaded by the agent job to `/tmp/binlogs`, and mounted
# read-only into this container at `/data/binlogs` by the gh-aw MCP gateway.
#
# NOT pinned by digest, and that is a gh-aw v0.77.5 limitation, not a choice.
# This container is handed the binlogs of an unmerged, possibly external PR and
# its output is what the agent reports back, so "whatever this tag points at
# today" is a supply-chain decision made by whoever last pushed the tag — and
# the tag does move: it resolved to sha256:9f1e2c3e8281... from 2026-07-16
# until 2026-08-03, when it became
# sha256:ee7b7e5c6e162f3f0061822aa7183260626f1a1e986d04ba9915ab197a37932c.
# v0.77.5 validates `container` against `^[a-zA-Z0-9][a-zA-Z0-9/:_.-]*$`, which
# has no `@`, so `image@sha256:...` is rejected at compile time and the
# generated `download_docker_images.sh` pulls this image by bare tag while every
# other image in the lock is digest-pinned. gh-aw >= v0.83.x resolves and pins
# the digest automatically (verified: microsoft/testfx on v0.83.4 emits
# `digest` + `pinned_image` in its `gh-aw-manifest` and pulls by `@sha256:`), so
# this is fixed by bumping the compiler this repo pins rather than by editing
# this line.
# Refresh/inspect the current digest with:
#   docker buildx imagetools inspect \
#     mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64
mcp-servers:
  binlog-mcp:
    container: "mcr.microsoft.com/dotnet-buildtools/prereqs:azurelinux-3.0-binlog-mcp-amd64"
    mounts:
      - "/tmp/binlogs:/data/binlogs:ro"
    allowed: ["*"]

# Custom job that reuses the binlogs from the failed Azure DevOps build instead
# of rebuilding. It resolves the ADO build id (from the check details URL or
# the dispatch input), verifies the PR targets an in-scope base branch,
# downloads every `Logs_Build_*` artifact, extracts each leg's `*.binlog`, and
# uploads them for the agent job.
jobs:
  fetch-binlog:
    name: Fetch binlogs (Azure Pipelines)
    runs-on: ubuntu-latest
    timeout-minutes: 15
    # `check_run` fires for every check; only act on the arcade PR build check
    # reporting failure (or a manual dispatch).
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
      pr-merge-sha: ${{ steps.fetch.outputs.pr-merge-sha }}
      pr-checkout-ref: ${{ steps.fetch.outputs.pr-checkout-ref }}
      base-ref: ${{ steps.fetch.outputs.base-ref }}
      push-blocked: ${{ steps.fetch.outputs.push-blocked }}
      ado-build-id: ${{ steps.fetch.outputs.ado-build-id }}
      ado-build-url: ${{ steps.fetch.outputs.ado-build-url }}
    steps:
      - name: Download binlogs from the failed Azure Pipelines build
        id: fetch
        shell: bash
        env:
          GH_TOKEN: ${{ github.token }}
          GH_AW_REPO: ${{ github.repository }}
          ADO_API: "https://dev.azure.com/dnceng-public/public/_apis"
          ADO_BUILD_UI: "https://dev.azure.com/dnceng-public/public/_build/results"
          # arcade-pr pipeline definition id in dnceng-public/public (used to
          # validate a dispatched build id belongs to the right pipeline).
          ADO_BUILD_DEFINITION_ID: "283"
          EVENT_NAME: ${{ github.event_name }}
          CHECK_DETAILS_URL: ${{ github.event.check_run.details_url }}
          CHECK_HEAD_SHA: ${{ github.event.check_run.head_sha }}
          CHECK_PR_NUMBER: ${{ github.event.check_run.pull_requests[0].number }}
          DISPATCH_BUILD_ID: ${{ inputs['ado-build-id'] }}
          DISPATCH_PR_NUMBER: ${{ inputs['pr-number'] }}
        run: |
          # Advisory + best-effort: on any gap emit binlog-found=false and the
          # agent pipeline stays inert.
          set +e
          set +o pipefail
          emit_none() { echo "binlog-found=false" >> "$GITHUB_OUTPUT"; exit 0; }

          # --- 1. Resolve the Azure DevOps build id ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            BUILD_ID="${DISPATCH_BUILD_ID}"
          else
            # details_url looks like: .../_build/results?buildId=NNN&view=...
            BUILD_ID=$(printf '%s' "${CHECK_DETAILS_URL}" | grep -oE 'buildId=[0-9]+' | head -1 | cut -d= -f2)
          fi
          echo "Azure DevOps build id: '${BUILD_ID}'"
          [ -z "${BUILD_ID}" ] && { echo "::warning::Could not resolve an ADO build id."; emit_none; }
          # The build id feeds directly into ADO API URLs below; require it to
          # be purely numeric (esp. on workflow_dispatch, where it is free-form
          # input) so a malformed value can't alter the request path/query.
          if ! printf '%s' "${BUILD_ID}" | grep -qE '^[0-9]+$'; then
            echo "::warning::Resolved ADO build id '${BUILD_ID}' is not numeric; refusing."; emit_none
          fi

          # Fetch the build metadata once, up front: it is the authoritative
          # source both for the PR number (via sourceBranch) and for the
          # definition/result/revision validated in step 4.
          build_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}?api-version=7.1")
          RESULT=$(printf '%s' "${build_json}" | jq -r '.result // empty')
          DEF_ID=$(printf '%s' "${build_json}" | jq -r '.definition.id // empty')
          SRC_BRANCH=$(printf '%s' "${build_json}" | jq -r '.sourceBranch // empty')
          # A PR build's sourceBranch is exactly `refs/pull/<n>/merge`, so it
          # identifies the PR unambiguously — unlike the commit->PRs API, which
          # can return several PRs in an unspecified order.
          BUILD_PR_NUM=$(printf '%s' "${SRC_BRANCH}" | sed -n 's#^refs/pull/\([0-9]\{1,\}\)/merge$#\1#p')

          # --- 2. Resolve the PR number + head SHA ---
          if [ "${EVENT_NAME}" = "workflow_dispatch" ]; then
            PR_NUMBER="${DISPATCH_PR_NUMBER}"
            HEAD_SHA=""
          else
            # Prefer the PR named by the build's own sourceBranch (authoritative:
            # `refs/pull/<n>/merge`) over check_run.pull_requests[0], whose order
            # isn't guaranteed and can name a different PR that shares the commit.
            PR_NUMBER="${BUILD_PR_NUM:-${CHECK_PR_NUMBER}}"
            HEAD_SHA="${CHECK_HEAD_SHA}"
          fi
          [ -z "${PR_NUMBER}" ] && { echo "::warning::Could not resolve a PR number."; emit_none; }
          # PR_NUMBER feeds `gh api .../pulls/<n>` and the `refs/pull/<n>/merge`
          # comparison; require it numeric so a malformed value can't reach the
          # GitHub API path (traversal-like input) or skew the branch match.
          if ! printf '%s' "${PR_NUMBER}" | grep -qE '^[0-9]+$'; then
            echo "::warning::Resolved PR number '${PR_NUMBER}' is not numeric; refusing."; emit_none
          fi

          # --- 3. Scope check: only analyse PRs targeting main / release/* ---
          PR_JSON=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          BASE_REF=$(printf '%s' "${PR_JSON}" | jq -r '.base.ref // empty')
          # An empty BASE_REF means the `gh api` call failed or returned no
          # data (rate limit / transient error), NOT that the PR targets an
          # out-of-scope branch. Treat it as a data-resolution failure so a
          # valid PR isn't silently skipped and misreported as base '' out of
          # scope.
          [ -z "${BASE_REF}" ] && { echo "::warning::Could not resolve the base ref for PR #${PR_NUMBER} (GitHub API returned no data); treating as a data-resolution failure, not an out-of-scope branch."; emit_none; }
          [ -z "${HEAD_SHA}" ] && HEAD_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          case "${BASE_REF}" in
            main|release/*) echo "PR #${PR_NUMBER} base '${BASE_REF}' is in scope." ;;
            *) echo "::warning::PR #${PR_NUMBER} base '${BASE_REF}' is out of scope (main, release/*); skipping."; emit_none ;;
          esac

          # --- 3b. Resolve the ref the agent job should check out ---
          # The agent edits the PR's tree in place when it can push a fix, so it
          # needs the PR revision — not `check_run`'s ref, which is the default
          # branch. Two cases:
          #   * same-repo PR (dependency flow, maintainer branches): check out
          #     the head BRANCH BY NAME. gh-aw derives the push target from
          #     `git rev-parse --abbrev-ref HEAD`, so a detached checkout would
          #     report `HEAD` and break bundle generation.
          #   * fork PR: that branch does not exist here, so use the read-only
          #     `refs/pull/<n>/head`. Detached is fine — gh-aw refuses pushes to
          #     fork branches anyway, so those runs stay comment-only.
          HEAD_REPO=$(printf '%s' "${PR_JSON}" | jq -r '.head.repo.full_name // empty')
          HEAD_REF=$(printf '%s' "${PR_JSON}" | jq -r '.head.ref // empty')
          if [ -n "${HEAD_REF}" ] && [ "${HEAD_REPO}" = "${GH_AW_REPO}" ]; then
            CHECKOUT_REF="${HEAD_REF}"
          else
            CHECKOUT_REF="refs/pull/${PR_NUMBER}/head"
          fi
          echo "Agent checkout ref: '${CHECKOUT_REF}' (head repo '${HEAD_REPO}')"

          # --- 3c. Trusted loop guard for the push escape hatch ---
          # The analyst is told not to push a second fix (Step 6b), but an
          # instruction is not enforcement, and neither is anything installed
          # inside the agent's sandbox. So the decision is made here, in trusted
          # workflow code, and is applied by the job-level `if:` further up:
          # when this output is `true` the activation and agent jobs never run,
          # and `safe_outputs` is skipped with them, so no push is even
          # reachable.
          #
          # The condition is "the branch tip is itself an automated fix": the
          # previous attempt is the newest thing on the branch and the build
          # still fails, so it did not converge and a human has to take over.
          # Scoping it to the tip rather than to the whole history means the
          # workflow resumes the moment anyone pushes anything else, instead of
          # abandoning the pull request forever after one attempt.
          #
          # The `[build-failure-analysis]` marker is not written by the model:
          # the workflow sets `commit-title-suffix`, so gh-aw's push handler
          # appends it to the commit title while applying the patch. A guard
          # that depended on the agent remembering to write its own marker
          # would not be a guard.
          #
          # Fails closed: an unreadable commit blocks the escape hatch.
          PUSH_BLOCKED=true
          PR_TIP_SHA=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          if [ "${HEAD_REPO}" != "${GH_AW_REPO}" ]; then
            # gh-aw refuses pushes to fork branches, so the loop guard is moot
            # here and must not suppress the (comment-only) analysis.
            PUSH_BLOCKED=false
          elif [ -n "${CHECK_PR_NUMBER}" ] && [ "${CHECK_PR_NUMBER}" != "${PR_NUMBER}" ]; then
            # The push target is bound to `check_run.pull_requests[0].number`
            # (see the `safe-outputs` block), while everything else keys off
            # PR_NUMBER, which prefers the ADO build's own source branch. Those
            # agree in practice, but if they ever disagree the guard below would
            # be checking one pull request while a push landed on another, so
            # the loop would no longer be bounded. Refuse the run instead.
            echo "::warning::The check payload names PR #${CHECK_PR_NUMBER} but the Azure Pipelines build belongs to PR #${PR_NUMBER}; skipping this run because the push target and the loop guard would disagree."
          elif [ -z "${PR_TIP_SHA}" ]; then
            echo "::warning::Could not resolve the head commit of PR #${PR_NUMBER}; skipping this run rather than risking a repeated automated fix."
          elif TIP_SUBJECT=$(gh api "repos/${GH_AW_REPO}/commits/${PR_TIP_SHA}" --jq '.commit.message | split("\n")[0]'); then
            if printf '%s' "${TIP_SUBJECT}" | grep -qF '[build-failure-analysis]'; then
              echo "::warning::PR #${PR_NUMBER}'s tip commit is an automated [build-failure-analysis] fix and the build still fails, so the automated fix is not converging; skipping this run and leaving the pull request to a human. Any further commit on the branch re-enables the analysis."
            else
              PUSH_BLOCKED=false
            fi
          else
            echo "::warning::Could not read commit ${PR_TIP_SHA} of PR #${PR_NUMBER}; skipping this run rather than risking a repeated automated fix."
          fi

          # --- 4. Validate the build for EVERY trigger (not just dispatch):
          #        it must be the arcade-pr definition (283), have failed, and
          #        belong to this PR (sourceBranch == refs/pull/<PR>/merge).
          #        For `check_run` the build id is parsed from a check payload
          #        we don't fully trust; for dispatch the build id and PR
          #        number are independent inputs. Validating on both paths
          #        prevents downloading an unrelated build or posting its
          #        analysis to the wrong PR.
          echo "ADO build ${BUILD_ID}: result='${RESULT}' definition='${DEF_ID}' sourceBranch='${SRC_BRANCH}'"
          if [ "${DEF_ID}" != "${ADO_BUILD_DEFINITION_ID}" ]; then
            echo "::warning::ADO build ${BUILD_ID} is definition '${DEF_ID}', not arcade-pr (${ADO_BUILD_DEFINITION_ID}); refusing."; emit_none
          fi
          if [ "${RESULT}" != "failed" ]; then
            echo "::warning::ADO build ${BUILD_ID} did not fail (result='${RESULT}'); nothing to analyze."; emit_none
          fi
          if [ "${SRC_BRANCH}" != "refs/pull/${PR_NUMBER}/merge" ]; then
            echo "::warning::ADO build ${BUILD_ID} sourceBranch '${SRC_BRANCH}' does not match PR #${PR_NUMBER} (refs/pull/${PR_NUMBER}/merge); refusing to avoid posting to the wrong PR."; emit_none
          fi

          # Require the build's analyzed revision to equal the PR's CURRENT
          # head. gh-aw safe-output review comments carry no `commit_id` — they
          # target the current PR diff — so analyzing a stale revision would
          # produce inline suggestions that get rejected or land on the wrong
          # lines. If the PR has advanced since this build ran, skip: a newer
          # build/check for the current head will cover it.
          BUILD_PR_SHA=$(printf '%s' "${build_json}" | jq -r '.triggerInfo["pr.sourceSha"] // empty')
          CURRENT_HEAD=$(printf '%s' "${PR_JSON}" | jq -r '.head.sha // empty')
          # ADO builds GitHub's `refs/pull/<n>/merge` ref, so build_json.sourceVersion
          # is the merge commit GitHub produced at build time and equals the PR's
          # `merge_commit_sha` then. If the base branch advances (even with the PR
          # head unchanged) GitHub recomputes that merge and merge_commit_sha
          # changes, so this catches base-advance staleness the head check misses.
          BUILD_MERGE_SHA=$(printf '%s' "${build_json}" | jq -r '.sourceVersion // empty')
          CURRENT_MERGE=$(printf '%s' "${PR_JSON}" | jq -r '.merge_commit_sha // empty')
          # Fail CLOSED: if either the build's analyzed revision or the current
          # PR head can't be resolved, skip — we must not analyze a possibly
          # stale binlog against the current diff (inline comments have no
          # commit_id and target the current PR diff).
          if [ -z "${BUILD_PR_SHA}" ] || [ -z "${CURRENT_HEAD}" ]; then
            echo "::warning::Could not resolve build revision ('${BUILD_PR_SHA}') and/or current PR head ('${CURRENT_HEAD}'); skipping to avoid analyzing a stale binlog against the current diff."
            emit_none
          fi
          if [ "${BUILD_PR_SHA}" != "${CURRENT_HEAD}" ]; then
            echo "::warning::Build ${BUILD_ID} analyzed revision '${BUILD_PR_SHA}' but PR #${PR_NUMBER} head is now '${CURRENT_HEAD}'; skipping stale build (a newer build/check will cover the current revision)."
            emit_none
          fi
          # When both merge revisions are known and differ, the base branch moved
          # since the build — the binlog reflects an obsolete merge. Skip.
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${CURRENT_MERGE}" ] && [ "${BUILD_MERGE_SHA}" != "${CURRENT_MERGE}" ]; then
            echo "::warning::Build ${BUILD_ID} merge revision '${BUILD_MERGE_SHA}' but PR #${PR_NUMBER} current merge is '${CURRENT_MERGE}' (base branch advanced); skipping stale merge."
            emit_none
          fi
          # Consistent now: build revision == current PR head. Use it for
          # permalinks so they line up with the inline comments' diff target.
          HEAD_SHA="${CURRENT_HEAD}"
          echo "Analyzing build ${BUILD_ID} at PR head revision '${HEAD_SHA}'."

          # --- 5. Download every Logs_Build_* artifact and extract binlogs ---
          artifacts_json=$(curl -sSL --retry 3 "${ADO_API}/build/builds/${BUILD_ID}/artifacts?api-version=7.1")
          mapfile -t names < <(printf '%s' "${artifacts_json}" | jq -r '.value // [] | map(select(.name | test("^Logs_Build_"))) | .[].name')
          [ "${#names[@]}" -eq 0 ] && { echo "::warning::No Logs_Build_* artifacts on build ${BUILD_ID}."; emit_none; }

          # Guards for untrusted PR-produced archives: cap the compressed
          # download and the reported uncompressed size per artifact, bound
          # extraction time, AND enforce a cumulative uncompressed budget across
          # all legs so many individually-small artifacts can't collectively
          # exhaust the runner's disk.
          MAX_ZIP_BYTES=524288000       # 500 MB compressed per artifact
          MAX_UNZIP_BYTES=2147483648    # 2 GB uncompressed per artifact
          MAX_TOTAL_BYTES=4294967296    # 4 GB uncompressed across all artifacts
          TOTAL_BYTES=0
          mkdir -p /tmp/binlogs
          count=0
          staged_legs=0
          ai=0
          for name in "${names[@]}"; do
            # `name` is PR-controlled ADO artifact metadata and the
            # `^Logs_Build_` filter only anchors the prefix, so sanitize it
            # before using it in any on-disk path (guards against `/` or `..`
            # traversal); keep the original `name` for the artifacts_json lookup.
            safe_name=$(printf '%s' "${name}" | tr -c 'A-Za-z0-9._-' '_')
            ai=$((ai + 1))
            url=$(printf '%s' "${artifacts_json}" | jq -r --arg n "${name}" '.value[] | select(.name==$n) | .resource.downloadUrl // empty')
            [ -z "${url}" ] && continue
            rm -rf /tmp/ax /tmp/a.zip
            mkdir -p /tmp/ax
            # Hard-cap the bytes written to disk regardless of Content-Length:
            # stream through `head -c` (cap + 1) and bound total time. This
            # closes the gap where `curl --max-filesize` alone would let a
            # length-less response write unbounded data before any post-check.
            # `curl_rc` captures curl's OWN status: the pipeline's status is
            # `head`'s (which succeeds even when curl dies mid-transfer), so a
            # timed-out or interrupted download would otherwise be invisible
            # here and only resurface below as an unreadable archive —
            # indistinguishable from a hostile one, and costing the whole leg
            # and with it the entire analysis (see the all-legs guard below).
            # Nothing is appended to the pipeline and errexit is not toggled
            # around it: this step already runs with errexit off (`set +e` at
            # the top), so the pipeline cannot abort it, and a trailing
            # `|| true` would run `true` — itself a command — resetting
            # PIPESTATUS before curl's status could be read.
            curl -sSL --retry 3 --retry-delay 2 --max-time 600 "${url}" 2>/dev/null | head -c $((MAX_ZIP_BYTES + 1)) > /tmp/a.zip
            curl_rc=${PIPESTATUS[0]}
            ZIP_BYTES=$(stat -c%s /tmp/a.zip 2>/dev/null || echo 0)
            if [ "${ZIP_BYTES}" -eq 0 ]; then
              echo "::warning::Skipping ${name}: empty or failed download."; continue
            fi
            if [ "${ZIP_BYTES}" -gt "${MAX_ZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: download exceeded ${MAX_ZIP_BYTES} bytes."; continue
            fi
            # Only here can a non-zero curl status mean a genuinely failed
            # transfer: an oversized artifact makes `head` close the pipe early
            # and curl exit on SIGPIPE, and that case was just skipped above.
            if [ "${curl_rc}" -ne 0 ]; then
              echo "::warning::Skipping ${name}: download failed or was truncated (curl exit ${curl_rc})."; continue
            fi
            # Bound the probe with `timeout` so a hostile/huge archive can't
            # hang the runner. `unzip -Zt` prints ONE summary line
            # ("<n> files, <x> bytes uncompressed, <y> bytes compressed")
            # instead of `unzip -l`'s per-entry listing, so the total is read
            # from a fixed column rather than by `tail -1`-ing a table whose
            # last line shifts with the entry list. (Measured on real
            # `Logs_Build_*` legs both forms agree and both are fast — this is
            # a robustness and parsing change, not a speed fix; the observed
            # "failed or timed out" warnings came from unreadable archives,
            # which the curl status check above now reports accurately.) Run
            # the pipeline in a subshell with `pipefail` and FAIL CLOSED on a
            # non-zero exit: if `timeout` kills the probe, its partial output
            # can still end in a numeric column and undercount the total,
            # bypassing the size guard below — so a failed probe skips the
            # artifact rather than trusting the parsed value.
            UNCOMP=$(set -o pipefail; timeout 60 unzip -Zt /tmp/a.zip 2>/dev/null | awk '{print $3}') \
              || { echo "::warning::Skipping ${name}: 'unzip -Zt' failed or timed out; cannot verify uncompressed size."; continue; }
            # Fail safe: if the uncompressed size isn't a plain integer (corrupt
            # zip / unexpected or timed-out `unzip -Zt` output), we can't verify
            # it — skip the artifact rather than let a non-numeric value bypass
            # the `-gt` guard.
            if ! printf '%s' "${UNCOMP}" | grep -qE '^[0-9]+$'; then
              echo "::warning::Skipping ${name}: could not determine uncompressed size (unparseable/timed-out unzip output)."; continue
            fi
            # ZIP64 uncompressed sizes can reach ~20 digits — beyond Bash's
            # signed 64-bit range, where `-gt` (and the cumulative `$((...))`
            # below) error out and, under `set +e`, would let an oversized
            # archive slip past the guard. Any value with more digits than the
            # limit is unambiguously larger, so reject on decimal length first;
            # after this, UNCOMP fits safely in the integer range used below.
            if [ "${#UNCOMP}" -gt "${#MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size has ${#UNCOMP} digits, exceeding the ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ "${UNCOMP}" -gt "${MAX_UNZIP_BYTES}" ]; then
              echo "::warning::Skipping ${name}: uncompressed size ${UNCOMP} exceeds ${MAX_UNZIP_BYTES} guard (possible zip bomb)."; continue
            fi
            if [ $((TOTAL_BYTES + UNCOMP)) -gt "${MAX_TOTAL_BYTES}" ]; then
              echo "::warning::Cumulative uncompressed budget ${MAX_TOTAL_BYTES} reached at ${name}; stopping extraction."; break
            fi
            # Refuse the archive if any entry path is absolute or has a `..`
            # component (defense-in-depth over unzip's own traversal guard),
            # then extract `*.binlog` entries *preserving* their in-archive
            # paths (no `-j`) under a fresh dir + timeout, so two binlogs that
            # share a basename in different folders don't overwrite each other.
            # Stream the entry listing through `grep` under a `timeout` (no full
            # in-memory buffer of entry names, which a many-entry archive could
            # bloat) and use PIPESTATUS to separate the failure modes: a
            # non-zero listing exit (error/timeout) FAILS CLOSED; a grep match
            # means a suspicious absolute/`..` path.
            timeout 60 unzip -Z1 /tmp/a.zip 2>/dev/null | grep -qE '(^/|(^|/)\.\.(/|$))'
            zscan_rc=("${PIPESTATUS[@]}")
            if [ "${zscan_rc[0]}" -ne 0 ]; then
              echo "::warning::Skipping ${name}: could not list archive entries (unzip -Z1 rc=${zscan_rc[0]})."; continue
            fi
            if [ "${zscan_rc[1]}" -eq 0 ]; then
              echo "::warning::Skipping ${name}: archive has a suspicious (absolute or ..) entry path."; continue
            fi
            timeout 120 unzip -o /tmp/a.zip '*.binlog' -d /tmp/ax >/dev/null 2>&1 \
              || { echo "::warning::Skipping ${name}: extraction failed or timed out."; continue; }
            # Consume the cumulative budget only once the archive actually
            # extracted — not on a suspicious-path or extraction-failure skip
            # above — so a skipped leg can't wrongly exhaust the budget and
            # force later legs to be dropped as "incomplete".
            TOTAL_BYTES=$((TOTAL_BYTES + UNCOMP))
            i=0
            leg_staged=0
            while IFS= read -r bl; do
              [ -f "${bl}" ] || continue
              # Every destination is uniquely prefixed with the artifact index
              # (`ai`) and a per-file counter (`i`), so neither a cross-artifact
              # sanitize collision nor same-basename entries within one archive
              # can overwrite a previously staged leg's binlog. `safe_name` is
              # kept only for readability.
              dest="/tmp/binlogs/${ai}_${i}_${safe_name}.binlog"
              # Only count a staged binlog when the copy actually succeeds —
              # `set +e` is on, so a failed `cp` must not inflate the counts.
              if cp "${bl}" "${dest}"; then
                count=$((count + 1))
                i=$((i + 1))
                leg_staged=1
              else
                echo "::warning::Failed to stage ${bl}; skipping."
              fi
            done < <(find /tmp/ax -type f -name '*.binlog')
            # This leg produced at least one usable binlog.
            [ "${leg_staged}" -eq 1 ] && staged_legs=$((staged_legs + 1))
          done
          echo "Extracted ${count} binlog(s) from ${staged_legs}/${#names[@]} legs into /tmp/binlogs:"
          ls -la /tmp/binlogs || true
          [ "${count}" -eq 0 ] && { echo "::warning::No *.binlog found in any Logs_Build_* artifact of build ${BUILD_ID}."; emit_none; }
          # Fail CLOSED on a partial set: if any Logs_Build_* leg did not yield
          # a usable binlog (download/extract failure, size-guard skip, or no
          # binlog inside), we cannot see every leg. Activating anyway would let
          # the agent treat the retrieved legs as the whole build and possibly
          # mis-classify a real build break in a missing leg as a clean compile /
          # non-build failure. A later build/check will re-trigger the analysis.
          if [ "${staged_legs}" -ne "${#names[@]}" ]; then
            echo "::warning::Only ${staged_legs} of ${#names[@]} Logs_Build_* legs produced a usable binlog; skipping to avoid analyzing an incomplete build (a missing leg could be the one that failed)."
            emit_none
          fi

          # The download/extract loop above can take minutes. Re-read the PR
          # head right before activating and fail CLOSED if it moved or can't
          # be resolved: a force-push during that window would otherwise leave
          # the analyzed binlog stale relative to the current diff (inline
          # comments carry no commit_id and target the current diff).
          LATEST_PR=$(gh api "repos/${GH_AW_REPO}/pulls/${PR_NUMBER}" 2>/dev/null)
          LATEST_HEAD=$(printf '%s' "${LATEST_PR}" | jq -r '.head.sha // empty')
          LATEST_MERGE=$(printf '%s' "${LATEST_PR}" | jq -r '.merge_commit_sha // empty')
          if [ -z "${LATEST_HEAD}" ] || [ "${LATEST_HEAD}" != "${HEAD_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} head changed during artifact download ('${HEAD_SHA}' -> '${LATEST_HEAD}') or could not be re-resolved; skipping to avoid posting stale-build suggestions against the new diff."
            emit_none
          fi
          # The base branch may also have advanced during the download; if the
          # merge revision moved from what the build analyzed, skip (stale merge).
          if [ -n "${BUILD_MERGE_SHA}" ] && [ -n "${LATEST_MERGE}" ] && [ "${LATEST_MERGE}" != "${BUILD_MERGE_SHA}" ]; then
            echo "::warning::PR #${PR_NUMBER} merge revision changed during artifact download ('${BUILD_MERGE_SHA}' -> '${LATEST_MERGE}'); skipping stale merge."
            emit_none
          fi

          {
            echo "binlog-found=true"
            echo "pr-number=${PR_NUMBER}"
            echo "pr-head-sha=${HEAD_SHA}"
            echo "pr-merge-sha=${BUILD_MERGE_SHA}"
            echo "pr-checkout-ref=${CHECKOUT_REF}"
            echo "base-ref=${BASE_REF}"
            echo "push-blocked=${PUSH_BLOCKED}"
            echo "ado-build-id=${BUILD_ID}"
            echo "ado-build-url=${ADO_BUILD_UI}?buildId=${BUILD_ID}"
          } >> "$GITHUB_OUTPUT"

      - name: Upload analysis artifact
        if: steps.fetch.outputs.binlog-found == 'true'
        uses: actions/upload-artifact@v7.0.1
        with:
          name: build-failure-analysis-data
          path: /tmp/binlogs
          if-no-files-found: warn
          retention-days: 1

# Steps that run in the agent job. Because the top-level `if:` gates activation
# on `needs.fetch-binlog.outputs.binlog-found == 'true'`, these only run once
# binlogs have been retrieved from the failed Azure DevOps build.
steps:
  - name: Download analysis artifact
    uses: actions/download-artifact@v8.0.1
    with:
      name: build-failure-analysis-data
      path: /tmp/binlogs

  - name: Export agent context
    shell: bash
    env:
      GH_AW_BINLOG_FOUND_VALUE: ${{ needs.fetch-binlog.outputs.binlog-found }}
      GH_AW_PR_NUMBER_VALUE: ${{ needs.fetch-binlog.outputs.pr-number }}
      GH_AW_PR_HEAD_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-head-sha }}
      GH_AW_PR_MERGE_SHA_VALUE: ${{ needs.fetch-binlog.outputs.pr-merge-sha }}
      GH_AW_ADO_BUILD_URL_VALUE: ${{ needs.fetch-binlog.outputs.ado-build-url }}
      GH_AW_GITHUB_WORKSPACE: ${{ github.workspace }}
    run: |
      # The binlogs are mounted into the binlog-mcp container at
      # `/data/binlogs`. Build the list of in-container binlog paths (one per
      # build leg) that the agent should query. `GH_AW_BINLOG_PATH` is the
      # first entry for tools/prompts that expect a single path.
      BINLOG_DIR="/data/binlogs"
      LIST=""
      if [ "${GH_AW_BINLOG_FOUND_VALUE:-false}" = "true" ] && [ -d /tmp/binlogs ]; then
        for f in /tmp/binlogs/*.binlog; do
          [ -f "$f" ] || continue
          LIST="${LIST}${BINLOG_DIR}/$(basename "$f")"$'\n'
        done
      fi
      FIRST=$(printf '%s' "$LIST" | head -1)
      {
        echo "GH_AW_BUILD_OUTCOME=failure"
        echo "GH_AW_BINLOG_DIR=${BINLOG_DIR}"
        echo "GH_AW_BINLOG_PATH=${FIRST}"
        echo "GH_AW_BINLOG_HOST_PATH=${GH_AW_ADO_BUILD_URL_VALUE}"
        echo "GH_AW_PR_NUMBER=${GH_AW_PR_NUMBER_VALUE}"
        echo "GH_AW_PR_HEAD_SHA=${GH_AW_PR_HEAD_SHA_VALUE}"
        echo "GH_AW_PR_MERGE_SHA=${GH_AW_PR_MERGE_SHA_VALUE}"
        echo "GH_AW_WORKSPACE=${GH_AW_GITHUB_WORKSPACE}"
        echo "GH_AW_BINLOG_LIST<<GH_AW_EOF"
        printf '%s' "$LIST"
        echo "GH_AW_EOF"
      } >> "$GITHUB_ENV"

tools:
  github:
    toolsets: [pull_requests, repos]
  # Needed to author the fix commit for `push-to-pull-request-branch`: the agent
  # edits the failing file in the checked-out PR tree and commits it locally,
  # and gh-aw packages those commits as the patch.
  #
  # NOTE: enabling `push-to-pull-request-branch` makes the **compiler** widen the
  # generated shell allowlist on its own with `git branch/checkout/merge/rm/
  # switch` — see the `--allow-tool` list in the compiled lock. Those come from
  # gh-aw, not from the list below, and cannot be removed from here. What matters
  # is that `git push` is not among them: the agent can never write to the
  # remote. The push happens in the `safe_outputs` job, from a bundle of the
  # agent's local commits, filtered by `allowed-files`. The analyst playbook
  # (Step 6b) forbids the agent from using the injected branch/history commands.
  edit:
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
    # Just enough git to inspect, stage and commit the fix. `push`, `reset` and
    # `rebase` are absent here and are not injected by the compiler either.
    - "git status:*"
    - "git diff:*"
    - "git log:*"
    - "git rev-parse:*"
    - "git add:*"
    - "git commit:*"
    # binlog-mcp is also mounted as a CLI wrapper (…/mcp-cli/bin/binlog-mcp);
    # allow it so the agent can query the binlogs via the wrapper when it does
    # not call the MCP tool natively.
    - "binlog-mcp:*"

safe-outputs:
  messages:
    footer: "> 🤖 **Automated content by GitHub Copilot.** Generated by the [{workflow_name}]({agentic_workflow_url}) workflow.{ai_credits_suffix} · [◷]({history_link})"
  # `check_run` carries no native issue/PR context for gh-aw, so the agent must
  # target the resolved PR explicitly (`target: "*"`) using `GH_AW_PR_NUMBER`.
  report-failure-as-issue: false
  add-comment:
    max: 5
    target: "*"
    # Hiding superseded comments is scoped to the posting workflow's id
    # (`GH_AW_WORKFLOW_ID`, the workflow FILE stem — here
    # `build-failure-analysis` vs `build-failure-analysis-command`), so this
    # workflow only ever hides its own comments: a re-run via
    # `/analyze-build-failure` leaves this stale automatic analysis visible next
    # to the fresh one. dotnet/sdk fixes that with the object form
    # (`hide-older-comments: {enabled: true, match: [...]}`), but the gh-aw
    # v0.77.5 schema this repo pins types `hide-older-comments` as a
    # `templatable_boolean` with no object/`match` variant, so it cannot be
    # expressed here. Fixed by bumping the compiler, not by editing this line.
    hide-older-comments: true
  create-pull-request-review-comment:
    max: 25
    target: "*"
  # Escape hatch for the case inline suggestions structurally cannot cover: a
  # `suggestion` block is only accepted by GitHub on lines that are part of the
  # PR diff, so when the root-cause fix lives in a file the PR never touched
  # (the classic dependency-flow break — a flowed package changes an API and the
  # unchanged call sites stop compiling) the analysis could previously only
  # describe the fix and ask a maintainer to commit it by hand. This lets the
  # agent append the fix commit to the PR branch instead.
  #
  # Guardrails, in order of how much they actually protect:
  #   * The push target is bound to the pull request in the `check_run`
  #     webhook payload rather than to a number the agent supplies, so the
  #     agent cannot redirect the push at another pull request. Because GitHub
  #     leaves that field empty for fork-originated check runs, fork pull
  #     requests have no push target at all — on top of which gh-aw's handler
  #     refuses fork branches outright (the workflow token has no write access
  #     to a fork). So this only ever reaches same-repo branches — i.e.
  #     dependency-flow (`darc-*`) branches and branches from people who
  #     already have write access. It is append-only; force-push is impossible.
  #   * `allowed-files` is an exclusive allowlist: anything outside `src/` is
  #     refused by the handler regardless of what the agent produced. Build
  #     infrastructure (`eng/`, `global.json`, `.github/`, `NuGet.config`) is
  #     therefore out of reach, and `protected-files` stays at its default
  #     `blocked` policy on top of that.
  #   * `max: 1` bounds a single run; the fail → push → rebuild → fail loop is
  #     bounded by trusted code rather than by model compliance. `commit-title-
  #     suffix` (with `patch-format: am`, the only transport on which the
  #     handler rewrites commit titles) makes gh-aw's push handler stamp
  #     `[build-failure-analysis]` onto the commit title as it applies the
  #     patch — the marker is written by the handler, never by the model —
  #     and the fetch job (step 3c) refuses to
  #     activate the workflow at all when the branch tip already carries it.
  #     Because the activation and agent jobs are skipped, gh-aw's own
  #     `safe_outputs` job (conditioned on the agent not being skipped) is
  #     skipped too, so no push code path remains. The agent playbook explains
  #     the rule, but nothing depends on the agent honouring it.
  #     This matters because our push is made with GITHUB_TOKEN — which does not
  #     re-trigger GitHub Actions — but Azure DevOps' GitHub app *does* rebuild,
  #     so a new run can follow every push.
  #     The guard is scoped to the branch *tip*, not to the whole history, so a
  #     pull request is not abandoned forever after one automated attempt: any
  #     later commit by anyone restores full analysis.
  #     Note the optional `GH_AW_CI_TRIGGER_TOKEN` magic secret (gh-aw wires it
  #     into the generated lock unconditionally) is deliberately NOT configured:
  #     it exists only to push an extra empty commit so *Actions* CI re-triggers.
  #     Unset, the expression resolves to an empty string and that step is
  #     skipped, so this workflow has no new secret prerequisite — and our CI is
  #     Azure DevOps, which rebuilds on its own.
  # `fallback-as-pull-request: false` keeps a diverged branch from silently
  # turning into a surprise PR (and drops the extra `pull-requests: write`
  # requirement); `check-branch-protection: false` avoids needing
  # `administration: read` just for a pre-flight the platform enforces anyway.
  push-to-pull-request-branch:
    max: 1
    # Deliberately NOT `target: "*"`. With `*`, gh-aw's handler takes the pull
    # request number from the agent's own tool call, and only then checks
    # whether *that* pull request is a fork — so the number is model-controlled
    # and a prompt injection (build log, source comment, PR description) could
    # aim the push at an unrelated same-repo pull request. Binding it to the
    # check payload removes the choice: the number comes from GitHub's own
    # webhook, is never routed through the model, and the handler rejects
    # anything else.
    # This also disables the escape hatch on fork pull requests at no extra
    # cost: GitHub omits `pull_requests` for check runs on fork-originated
    # commits, so the expression resolves to an empty string and no push target
    # exists at all (verified against live `arcade-pr` check runs — same-repo
    # pull requests report exactly one entry, the fork ones report none). The
    # comment-only analysis is unaffected, which is the whole point of keeping
    # this gate here instead of in the job-level `if:`.
    target: "${{ github.event.check_run.pull_requests[0].number || inputs['pr-number'] }}"
    allowed-files:
      - "src/**"
    commit-title-suffix: " [build-failure-analysis]"
    # Required for `commit-title-suffix` to do anything: `patch-format`
    # defaults to `bundle`, and the handler only rewrites commit titles on the
    # `git am` path. On the default transport the marker would never be
    # applied, and the loop guard that keys off it would never fire.
    patch-format: am
    if-no-changes: "ignore"
    ignore-missing-branch-failure: true
    fallback-as-pull-request: false
    check-branch-protection: false
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

# Signing Graph Specification

This document specifies the behavior, state-machine, and state-transitions of the recursive signing dependency graph.

## Goals

- Model signing dependencies between files and containers.
- Enforce the constraint: **a container may only be repacked/signed after all signable children are signed (or skipped)**.
- Support discovery-time changes that affect previously-decided signing actions (e.g., a container initially considered signed may need to be re-signed if an unsigned child is discovered).
- Centralize all graph-affecting state transitions in `SigningGraph`.

## Core types

- `FileNode`
  - Represents a file occurrence in the signing graph.
  - Can have a `Parent` (container) and `Children` (contents).
  - Has intrinsic signing classification produced by signature calculation:
    - the resolved certificate identifier (always computed), and
    - whether the file is already signed and therefore potentially skippable.
- `SigningGraph`
  - Owns the set of nodes and all state transitions.
  - Computes when nodes are eligible to be signed and when containers are eligible to be repacked.

## Node state

`FileNodeState` is a per-node state machine. Only `SigningGraph` is allowed to transition a node to a new state.

### States

- `PendingSigning`
  - Node is in the signing workflow but is not currently eligible to sign.
  - For containers, this typically means at least one child is not yet signed/skipped.

- `PendingRepack`
  - Node is a container that is not signable itself (or is not yet eligible to repack) but is tracked until repack eligibility can be determined.
  - A container is placed in `PendingRepack` when it contains at least one signable child that has not yet completed.

- `ReadyToSign`
  - Node is eligible to be signed in the current signing round.
  - Typically used for leaf nodes (non-containers) or nodes with no remaining prerequisites.

- `ReadyToRepack`
  - Node is a container and is eligible to be repacked and then signed.
  - This is reached once all signable children are signed or skipped.

- `Signed`
  - Node has been signed in the current run.

- `Skipped`
  - Node will not be signed in the current run.
  - This includes files that are already signed, explicitly ignored, or otherwise not signable.

### Terminal states

A node is considered **done** when it is in either:

- `Signed`, or
- `Skipped`

Additionally, a **non-signable container** with **no signable children** is treated as done for the purpose of graph completion. Such a node remains in `PendingRepack` for tracking.

## Signability model

A node is considered **signable** when it has a non-null certificate identifier (i.e., `node.CertificateIdentifier != null`).

The graph distinguishes between:

- **Certificate resolution** (always performed)
- **Signing participation** (may be skipped)

A node is considered **potentially skippable** when signature calculation indicates it is already signed.

Skipping is finalized during `FinalizeDiscovery()` using both the node's intrinsic information and the state/needs of its children:

- A **leaf** that is already signed may initialize to `Skipped`.
- A **container** that is already signed may initialize to `Skipped` **only if no descendant requires signing or repack**.

> This prevents the "already signed container" optimization from incorrectly skipping containers that must be modified because a nested file was signed/updated.

## Graph invariants

The graph enforces the following invariants:

1. **Parent/child relationship is consistent**
   - If `child.Parent == parent` then `parent.Children` contains `child`.

2. **Container completion gating**
   - A container may transition to `ReadyToRepack` only when all *signable* children are done.

3. **Centralized state changes**
   - A node’s `State` must only be updated by `SigningGraph`.

4. **Discovery can invalidate prior decisions**
   - Adding a signable child to a container that is `Signed` or `Skipped` can require the container to be reintroduced into the workflow.

## Operations

### `AddNode(node, parent)`

Adds a new node to the graph and wires it to a parent container (if provided).

#### Preconditions

- `node` is non-null.
- `parent` may be null.

#### Effects

1. `node` is appended to the graph’s node set.
2. If `parent != null`:
   - `node.Parent = parent`
   - `parent.Children.Add(node)`
3. The graph does not derive execution ordering (including container signable-child counts) during discovery.
   `SigningInfo` is treated as not yet finalized until `FinalizeDiscovery()`.
4. No execution state transitions are considered final until `FinalizeDiscovery()` is called.

> Rationale: This is the key mechanism that prevents the graph from assuming discovery-time decisions are final.

### `FinalizeDiscovery()`

Completes graph discovery and computes initial execution states.

#### Preconditions

- Must be called before any of: `GetNodesReadyForSigning()`, `GetContainersReadyForRepack()`, `MarkAsSigned(...)`, `MarkContainerAsRepacked(...)`, `IsComplete()`.

#### Effects

1. The graph computes a children-first ordering of nodes so that **each node is processed after all of its children** (including reference nodes).
2. In a single bottom-up pass over that ordering, the graph computes for each concrete `FileNode`:
   - Container child progress (`SignableChildCount`, `SignedOrSkippedSignableChildCount`) based on already-initialized child states.
   - The node's *initial execution state*, which may be one of: `Skipped`, `PendingSigning`, `PendingRepack`, `ReadyToSign`, `ReadyToRepack`.

Initial state rules:

- Leaf nodes
  - If not signable: `Skipped`
  - Else if already signed: `Skipped`
  - Else: `ReadyToSign`

- Container nodes
  - If any child (direct or transitive) will be signed or repacked in this run: container participates in the workflow
    - If all signable children are done: `ReadyToRepack`
    - Otherwise: `PendingRepack`
  - Else (no descendant work is needed):
    - If already signed: `Skipped`
    - Else if the container itself is signable: `ReadyToSign`
    - Else: `PendingRepack` (tracked but treated as complete)

### `GetNodesReadyForSigning()`

Returns the set of nodes whose current state is `ReadyToSign`.

- This operation only reads current state.
- It does not perform dependency checks.

### `GetContainersReadyForRepack()`

Returns the set of nodes whose current state is `ReadyToRepack`.

### `MarkAsSigned(node)`

Marks the specified node as signed and updates parent container progress.

#### Preconditions

- `node` is non-null.

#### Effects

1. `node.State` transitions to `Signed`.
2. If `node.Parent != null` and the node is signable:
   - Increment the parent’s `SignedOrSkippedSignableChildCount`.
   - If `SignedOrSkippedSignableChildCount >= SignableChildCount` and the parent is `PendingSigning`, transition the parent to `ReadyToRepack`.

### `MarkContainerAsRepacked(container)`

Transitions a container that has just been repacked from `ReadyToRepack` to `ReadyToSign`.

#### Rationale

`ReadyToRepack` is not a terminal state. Without an explicit transition after repack, a container can remain `ReadyToRepack` indefinitely, preventing the overall signing loop from reaching `IsComplete()`.

## State transition table

The following transitions are permitted by `SigningGraph`:

### During discovery (`AddNode`)

- No execution state transitions are defined during discovery.

### During discovery finalization (`FinalizeDiscovery`)

- `∅ → Skipped`
- `∅ → ReadyToSign` (leaf signable node or container with no signable children)
- `∅ → PendingSigning` (reserved for future use; the current implementation initializes signable leaves directly to `ReadyToSign`)
- `∅ → PendingRepack` (container waiting on signable children, or non-signable container with no signable children)
- `∅ → ReadyToRepack` (container whose signable children are already done)

### During signing

- `ReadyToSign → Signed` (`MarkAsSigned`)

### During repack

- `ReadyToRepack → ReadyToSign` (`MarkContainerAsRepacked`)

### Container gating

- `PendingRepack → ReadyToRepack` (all signable children are done)

## Notes and known gaps

- The state machine above describes existing behavior.
- If the pipeline needs to change a node’s `SigningInfo` after discovery (e.g., re-run the signature calculator with new context), the graph should expose an explicit API like `UpdateSigningInfo(node, newInfo)` that:
  - updates counters for the parent container,
  - re-evaluates node initial/derived state,
  - re-evaluates impacted ancestor containers.

Until such an API exists, callers should treat `SigningInfo` as immutable and add nodes in discovery order with correct `SigningInfo`.

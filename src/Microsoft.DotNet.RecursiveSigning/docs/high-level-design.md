# High-Level Design and Principles

## Purpose
Microsoft.DotNet.RecursiveSigning orchestrates recursive signing of artifacts so that each unique payload is signed exactly once while preserving container integrity. The component provides a single entry point for CI/CD systems that need deterministic, parallelizable signing flows across multiple container formats and signing services.

## Goals
- Reduce redundant signing operations by deduplicating identical files and only signing when necessary.
- Preserve the structure and metadata of containers while iteratively updating their contents with signed files.
- Offer consistent orchestration that works with multiple signing providers and container handlers.
- Provide diagnostics, telemetry, and validation needed for production signing pipelines.

## Recursive Workflow Overview
1. **Discovery**: Identify top-level artifacts, detect nested containers, and gather metadata required for signing decisions.
2. **Graph Finalization**: Freeze discovery and compute initial node states bottom-up (including which files are immediately ready to sign or repack).
3. **Signing Rounds**: Sign all nodes currently marked `ReadyToSign`, update the graph, and unlock containers for repack.
4. **Repacking**: Repack containers marked `ReadyToRepack`, then mark them ready for signing.
5. **Finalization**: Emit signed outputs, collect telemetry, and return a summary of successes and failures.

## Algorithm Walkthrough
1. **Input Preparation**
   - Normalize the list of top-level artifacts and allocate temporary working locations defined by configuration.
   - Initialize telemetry, logging, and cancellation hooks that remain active throughout the run.
2. **Container Expansion**
   - For each artifact, choose an appropriate container handler (if any) and stream entries without rewriting the entire archive.
   - Each entry produces immutable metadata that records hashes, structural location, and container lineage; this metadata feeds both deduplication and certificate resolution.
3. **Deduplication and Graph Construction**
   - Combine each file’s content hash with its filename to build unique content keys.
   - The signing graph stores parent/child relationships using these keys so the orchestrator knows which containers depend on which files.
   - Files encountered multiple times reuse the same node, enabling one signed copy to serve every container that references it.
   - Files encountered multiple times are only analyzed a single time and only extracted to disk a single time. If a duplicate file is discovered, the graph should be updated
     (indicating a location where a signed copy needs to be placed), but no further extraction or analysis is done.
4. **Certificate Resolution and Validation**
   - The signature calculator evaluates configuration rules in priority order (explicit overrides, strong-name, extension).
   - Certificate resolution is performed for every discovered file so downstream components (dedup, signing, telemetry) have a consistent certificate identifier.
   - Separately, signature calculation records whether the file is already signed and therefore *potentially* skippable.
   - Skipping of already-signed containers is not decided here; it is decided during graph finalization using child/descendant needs.
   - Failures halt the run before any signing occurs.
5. **Graph finalization (bottom-up)**
   - Once discovery is complete, the graph is finalized by processing nodes in a children-first order.
   - Each node's initial execution state is computed directly (for example, leaf signable nodes start as `ReadyToSign`).
   - Containers compute signable-child progress during this same pass, which determines whether they start as `PendingRepack` or `ReadyToRepack`.
   - Containers that are already signed may be initialized to `Skipped` *only when no descendant will be modified*; this prevents skipping a container that must be repacked because a nested file was signed.
6. **Iterative Signing + Repack rounds**
   - The orchestrator repeatedly:
     - Signs all nodes returned by `GetNodesReadyForSigning()`.
     - Repacks all containers returned by `GetContainersReadyForRepack()`.
     - Marks repacked containers ready to sign.
   - Successful signing marks nodes as `Signed` and updates parent container progress so containers can become eligible for repack.
7. **Completion and Reporting**
   - When no unsigned nodes remain, results are collated: signed file inventory, reused copies, validation issues, and telemetry trends.
   - The final report surfaces dedup hits, per-certificate statistics, and any containers that required special handling, enabling CI systems to correlate runs across builds.

## Architectural Pillars
- **Orchestration**: Coordinates discovery, graph updates, signing rounds, and telemetry.
- **Container Handling**: Abstracts reading from and writing to various archive formats while keeping metadata intact.
- **Certificate Resolution**: Chooses appropriate certificates by applying deterministic rules to file metadata.
- **Signing Execution**: Delegates actual signing or verification work to pluggable providers.
- **Content Deduplication**: Tracks signed artifacts by hash and filename to avoid re-signing identical content.

## Design Principles
- **Service Agnostic Rules**: Certificate selection logic does not depend on the signing service implementation.
- **Interface Driven**: Every external dependency (signing, containers, analysis) is accessed through dedicated abstractions for testability and extensibility.
- **Immutable Data Models**: Metadata captured during discovery is treated as immutable state that flows through the pipeline, reducing race conditions.
- **Async by Default**: All I/O heavy work uses asynchronous patterns to keep throughput high during large signing jobs.
- **Deterministic Ordering**: The signing graph ensures predictable signing rounds and reproducible outputs.
- **Observability**: Logging, telemetry, and validation are present throughout each phase to simplify triage.
- **Avoid Disk Hydration**: Where possible, files are never hydrated to disk; processing should prefer streams and in-memory buffers.
- **Minimize Temporary Files**: Temporary files should be avoided where possible, and are never written unless absolutely necessary for compatibility with external tools or formats.
- **In-Place Updates**: Signing and repack operations should be performed in place whenever supported by the file format and signing tooling.

## Extension Approach
- **New Signing Services**: Implement the signing provider and certificate identifier abstractions without touching orchestrator logic.
- **New Container Formats**: Register additional container handlers that know how to stream entries and write updated archives.
- **Custom Validation or Telemetry**: Plug in validators and telemetry collectors that tap into orchestration events.
- **Policy Overrides**: Provide alternative configurations or certificate rule sets to adapt to new compliance requirements.

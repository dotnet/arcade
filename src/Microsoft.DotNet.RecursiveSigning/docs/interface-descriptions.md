# Basic Interface Descriptions

Each interface exposes a narrowly scoped contract so that new implementations can be swapped in without editing the orchestrator. Below is a summary of the primary responsibilities, inputs, and outputs for every interface.

## IRecursiveSigning
- Serves as the public entry point for recursive signing requests.
- Accepts top-level artifacts plus configuration and drives the complete workflow.
- Emits a result object that summarizes signed files, errors, and telemetry.

## IContainerHandler
- Advertises whether it can process a given file extension or format.
- Streams container entries for discovery without forcing the orchestrator to extract to disk.
- Writes updated containers by combining existing metadata with newly signed files.

### Entry stream ownership and lifetime
- For discovery (`ReadEntriesAsync`): the orchestrator (caller) owns each returned `ContainerEntry` and must dispose it when finished. Disposing the entry disposes its `ContentStream`.
- Returned entry streams are only guaranteed to be valid until the corresponding `ContainerEntry` is disposed.
- For repacking (`WriteContainerAsync`): the orchestrator (caller) retains ownership of the passed `ContainerEntry` objects and their streams; handlers must not dispose entry streams.

## IContainerHandlerRegistry
- Stores the ordered list of available container handlers.
- Locates the first handler able to work with a specific artifact path.
- Enables runtime extensibility by allowing registration of custom handlers.

## IFileAnalyzer
- Inspects files to gather metadata needed for signing decisions (hashes, strong-name info, executable type, etc.).
- Detects whether a file is already signed and whether it contains nested content (this is an intrinsic property of the file as observed on disk).
- Produces immutable metadata objects consumed by downstream components.

## ISignatureCalculator
- Applies certificate rules (extension, strong-name, explicit overrides) to determine how a file must be signed.
- Always resolves the certificate identifier for every file (even if the file is already signed).
- Does not determine whether a file is already signed (that comes from `IFileAnalyzer`/metadata); it only determines what certificate would be used if signing is required.
- Validates that resolved certificates fit the file characteristics before signing.

## ISigningProvider
- Performs the actual signing, verification, or strong-name application using a backing service or toolchain.
- Batches files by certificate or operation to optimize service calls.
- Reports per-file status so the orchestrator can update progress and telemetry.

## IFileDeduplicator
- Tracks unique files by combining their hash and filename.
- Determines whether a signed copy already exists so it can be reused.
- Records which containers consume a given file to support later repacking.

## ISigningGraph
- Models container relationships and determines when a file is ready to sign.
- Provides the next set of signable files while ensuring dependencies are respected.
- Marks progress after signing so that repacking and subsequent rounds can proceed.

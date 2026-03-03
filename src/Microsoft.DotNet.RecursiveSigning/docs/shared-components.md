# Shared Misc Components

This document groups the supporting data models and services that multiple parts of the recursive signing pipeline rely upon.

## Configuration and Requests
- **SigningRequest**: Captures the input files, configuration object, and options for a signing run.
- **SigningConfiguration**: Provides temporary storage locations required during recursive signing.
- **SigningOptions**: Hosts execution toggles such as parallelism, strict validation levels, and logging verbosity.

## Certificate Rules
- **DefaultCertificateRules**: Stores file-name and extension mappings plus raw certificate definition JSON keyed by friendly name.
- **DefaultCertificateRulesReader**: Reads JSON configuration and materializes `DefaultCertificateRules`.
- **ICertificateCalculator**: Resolves `ICertificateIdentifier` values from file metadata and rules.
- **ICertificateIdentifier Implementations**: Represent certificates in a service-agnostic way so that the orchestrator can batch files without understanding provider specifics.

## File and Container Metadata
- **FileMetadata**: Immutable description of a file, including hashes, public key token, target framework, executable classification, and container-relative paths.
- **FileContentKey**: Combination of hash and filename used to deduplicate files across containers and signing rounds.
- **ContainerEntry**: Snapshot of an item inside a container, including its relative path, content location, metadata, and whether it has been updated with a signed version.
- **ContainerMetadata**: Captures format-specific attributes (timestamps, permissions, compression hints) that must be preserved when writing the container back out.

## Signing State
- **SigningRound / FileNode**: Graph nodes and batches that help the orchestrator enforce signing order and parallelism.
- **SigningResult / SigningError**: Aggregate outcome returned to callers, including success flag, per-file summaries, telemetry, and any validation failures.

## Telemetry and Validation Hooks
- **Telemetry Collectors**: Optional components that record metrics such as queue depth, signing duration, and dedup hits.
- **Validators**: Plug-ins that evaluate custom compliance rules before or after signing to ensure artifacts meet policy requirements.
- **Logging**: Structured logging surfaces are available across all major stages for correlation with CI/CD runs.

# Default Signature Calculator (Legacy Design Notes)

This document captures broader certificate-calculation concepts.  
For the current implementation design and data model, see [Default Certificate Calculator](default-certificarte-calculator.md).

## Responsibilities
- Resolve certificate identifiers in a deterministic way for each discovered file.
- Inspect `IFileMetadata` (including `FileName`) and apply configured rule precedence.
- Surface configuration errors when mappings target undefined certificates.

## Rule Evaluation Order
1. **File-name mappings**: Exact filename match has highest precedence.
2. **File-extension mappings**: Extension-based fallback when filename rule does not match.

The first matching rule produces the certificate identifier used for batching during signing rounds. Absence of a match means the file is not signed.

## Skip and Already-Signed Handling
- Already-signed status remains analyzer/graph-driven, not calculator-driven.
- Calculator output only determines which certificate identifier would be used when signing occurs.

## Outputs
- Produces an `ICertificateIdentifier` (or `null` when no rule matches).

## Extensibility Considerations
- Additional calculators can be introduced by implementing `ICertificateCalculator`.
- Calculators can share `DefaultCertificateRules` or provide alternative rule readers/models.


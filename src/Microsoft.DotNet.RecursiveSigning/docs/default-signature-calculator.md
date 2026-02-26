# Default Signature Calculator

The default implementation of `ISignatureCalculator` (referred to here as the DefaultSignatureCalculator) applies deterministic rules to choose the correct certificate configuration for every file processed by Microsoft.DotNet.RecursiveSigning.

## Responsibilities
- Interpret the service-agnostic `CertificateRules` supplied through the signing configuration.
- Inspect the `FileMetadata` provided by the analyzer to understand file type, strong-name information, and container relationships.
- Decide whether a file requires signing, can be skipped, or is already compliant.
- Surface clear validation errors when no certificate mapping is available for a required file.

## Rule Evaluation Order
1. **Explicit File Overrides**: Match on filename plus optional metadata to support bespoke signing requirements for critical binaries.
2. **Strong-Name Rules**: Match on public key token and optional target framework to handle assemblies that share file extensions but require different certificates.
3. **File Extension Rules**: Provide the fallback mapping for standard file types such as `.dll`, `.exe`, `.nupkg`, or archive formats.

The first matching rule produces the certificate identifier used for batching during signing rounds. Absence of a match yields a validation error unless the file is flagged to be ignored.

## Skip and Already-Signed Handling
- Consults the `FilesToSkipStrongNameCheck` and `FilesToSkip3rdPartyCheck` lists to respect policy exceptions.
- Uses analyzer metadata to determine if a file is already signed and whether that signature satisfies policy; if so, it marks the file as complete to avoid redundant work.
- Marks files as "ignored" when configuration explicitly states they do not require signing (for example, third-party dependencies supplied pre-signed).

## Outputs
- Produces a `SigningInfo` record containing the selected certificate identifier, skip flags, and already-signed status.
- Provides validation feedback consumed by the orchestrator so that misconfigurations stop the run early with actionable messages.

## Extensibility Considerations
- Additional calculators can be introduced when projects need alternative matching logic; they can reuse the same rules data or define new rule sets entirely.
- The default implementation is stateless aside from the provided rules, enabling straightforward unit testing and deterministic outcomes.

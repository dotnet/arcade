# Microsoft.DotNet.RecursiveSigning

Microsoft.DotNet.RecursiveSigning provides a modern, extensible pipeline for recursively signing artifacts such as NuGet packages, ZIP archives, and other containers. The orchestrator discovers nested content, deduplicates identical files, batches signing work, and repacks containers while preserving metadata.

## Documentation
- [High-Level Design and Principles](docs/high-level-design.md)
- [Basic Interface Descriptions](docs/interface-descriptions.md)
- [Shared Misc Components](docs/shared-components.md)
- [Default Signature Calculator](docs/default-signature-calculator.md)
- [Default File Deduplicator](docs/default-file-deduplicator.md)

These documents describe the architecture, responsibilities, and extensibility points without prescribing implementation details. Consult them when modifying or extending the recursive signing system.


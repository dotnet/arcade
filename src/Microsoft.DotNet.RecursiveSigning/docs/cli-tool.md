# CLI Tool Design

## Purpose
`Microsoft.DotNet.RecursiveSigning.Cli` provides a runnable end-to-end demonstration of recursive signing using:
- `DefaultCertificateRulesReader`
- `DefaultCertificateCalculator`
- `DryRunSigningProvider`

It is designed for local validation of calculator/rule behavior without invoking external signing services.

## Inputs
- `--config <rules.json>`: JSON file containing certificate definitions and rule mappings.
- `--input <file-or-glob>` (repeatable): top-level files to process. Glob patterns are expanded before execution.
- `--temp <directory>` (optional): temporary working directory.
- `--output <directory>` (optional): output directory for copied final root artifacts.

## Execution Flow
1. Parse command-line options.
2. Load rules via `DefaultCertificateRulesReader`.
3. Configure DI with:
   - `AddRecursiveSigning()`
   - `ZipContainerHandler`
   - `DefaultFileAnalyzer`
   - `DefaultCertificateCalculator`
   - `DryRunSigningProvider`
4. Build a `SigningRequest` (including optional output directory) and invoke `IRecursiveSigning.SignAsync`.
5. Print signed file summary and errors to console.

## Dry-Run Behavior
- `DryRunSigningProvider` copies candidate files to output paths and appends a dry-run marker.
- No external signing APIs are called.
- The flow exercises graph traversal, rule resolution, batching, and repack logic.

## Output Semantics
- Recursive signing updates working files in place.
- Nested/extracted files and repacked containers remain in their working locations and are never redirected by the CLI.
- When `--output` is specified, root inputs are first copied to the configured output directory and processing runs against those copies.

## Configuration Contract
The rules file must include:
- `certificates`: array of JSON objects, each with `friendlyName`.
- `rules.fileNameMappings`: exact filename to friendly name.
- `rules.fileExtensionMappings`: extension to friendly name.

Certificate entries are preserved as raw JSON and attached to `ESRPCertificateIdentifier` for downstream use.


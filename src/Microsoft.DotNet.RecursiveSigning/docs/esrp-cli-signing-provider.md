# ESRP CLI Signing Provider

## Overview

The `ESRPCliSigningProvider` implements `ISigningProvider` by invoking the ESRP CLI tool (`esrpcli.dll`) as a child process. It translates a batch of `FileNode` objects from the recursive signing graph into a single ESRP CLI invocation using the `SignBatches` JSON input format, which natively supports multiple groups of files with different signing operations in one call.

This provider is the production signing backend for the recursive signing workflow.

It also supports a **dry-run mode** that prints the JSON submission and CLI arguments that would be used, without invoking the ESRP CLI.

## Background: ESRP CLI

The ESRP CLI is a .NET tool invoked as:
```
dotnet esrpcli.dll vsts.sign [arguments]
```

It takes a JSON submission describing one or more `SignBatches` (each with files and signing operations), authentication credentials, and service configuration. It calls the ESRP gateway, waits for signing to complete, and reports results via stdout.

### Input JSON schema

The ESRP CLI accepts a JSON file (`-j`) with the following structure. Each `SignBatch` can contain a different set of files and signing operations, allowing files with different certificates to be signed in a single CLI invocation.

```json
{
  "Version": "1.0.0",
  "SignBatches": [
    {
      "SourceLocationType": "UNC",
      "SourceRootDirectory": "<rootDir>",
      "DestinationLocationType": "UNC",
      "DestinationRootDirectory": "<rootDir>",
      "SignRequestFiles": [
        {
          "CustomerCorrelationId": "<guid>",
          "SourceLocation": "<relativeFilePath>",
          "DestinationLocation": "<relativeFilePath>"
        }
      ],
      "SigningInfo": {
        "Operations": [
          {
            "KeyCode": "CP-230012",
            "OperationCode": "SigntoolSign",
            "Parameters": {
              "OpusName": "Microsoft",
              "OpusInfo": "http://www.microsoft.com",
              "FileDigest": "/fd \"SHA256\"",
              "PageHash": "/NPH",
              "TimeStamp": "/tr \"...\" /td sha256"
            },
            "ToolName": "sign",
            "ToolVersion": "1.0"
          }
        ]
      }
    }
  ]
}
```

### CLI arguments

| Flag | Purpose | Example |
|------|---------|---------|
| `-x` | Signing type | `regularSigning` |
| `-y` | Parameter type | `"inlineSignParams"` |
| `-c` | Max files in a batch | `400` |
| `-j` | Path to submission JSON file | `"<workDir>/submission.json"` |
| `-f` | Root directory (file paths are relative to this) | `"/build/workspace"` |
| `-p` | Path to pattern file (comma-separated relative paths) | `"<workDir>/pattern.txt"` |
| `-m` | Batch size | `400` |
| `-t` | Submission timeout (minutes) | `30` |
| `-v` | TLS version | `"Tls12"` |
| `-s` | ESRP gateway URL | `"https://api.esrp.microsoft.com/api/v2"` |
| `-o` | Organization name | `"Microsoft"` |
| `-i` | Organization info URL | `"https://www.microsoft.com"` |
| `-a` | ESRP client ID | `"dec434ad-..."` |
| `-d` | ESRP tenant ID | `"72f988bf-..."` |
| `-z` | Key vault JSON (vault name + cert name) | `{"akv":"...","cert":"..."}` |
| `-useMSIAuthentication` | Whether to use managed identity | `true`/`false` |
| `-federatedTokenData` | Federated token blob (federated auth) | JSON string |
| `-encryptedCertificateData` | Encrypted cert paths (cert auth) | JSON string |

### Result parsing

The ESRP CLI reports results via stdout:
- A line starting with `"Success"` → all files signed.
- A line containing `"failDoNotRetry"` → permanent failure.
- A line matching `"Calling esrp gateway get status for this operation Id: <guid>"` → operation ID.
- A non-zero exit code → failure regardless of stdout.

## Design

### Certificate identity

The provider works exclusively with `ESRPCertificateIdentifier`, which carries:
- `FriendlyName`: human-readable certificate name (e.g., `"MicrosoftAuthenticode"`).
- `CertificateDefinition`: a `JsonElement` containing the ESRP signing operations array. This is a drop-in for the `SigningInfo.Operations` field in the submission JSON.

If a file's `CertificateIdentifier` is not an `ESRPCertificateIdentifier`, the provider throws.

### Single invocation per `SignFilesAsync` call

The provider does **not** batch or group files itself. The ESRP CLI handles batching internally. A single `SignFilesAsync` call produces a single CLI invocation containing one `SignBatch` per distinct certificate. Files with the same certificate are grouped into the same batch.

### Core flow

```
SignFilesAsync(files, cancellationToken)
│
├── 1. Group files by ESRPCertificateIdentifier.FriendlyName
│
├── 2. Build submission JSON:
│   ├── For each certificate group, create one SignBatch entry:
│   │   ├── SourceLocationType = "UNC"
│   │   ├── SourceRootDirectory = RootDirectory
│   │   ├── DestinationLocationType = "UNC"
│   │   ├── DestinationRootDirectory = RootDirectory
│   │   ├── SignRequestFiles = [{ CustomerCorrelationId, SourceLocation, DestinationLocation }]
│   │   └── SigningInfo.Operations = certificate.CertificateDefinition (the raw JSON)
│   └── Wrap in { Version, SignBatches }
│
├── 3. If dry-run mode:
│   ├── Print submission JSON to logger/console
│   ├── Print CLI arguments that would be used
│   └── Return true
│
├── 4. Create unique working directory under TempDirectory
├── 5. Write submission JSON to <workDir>/submission.json
├── 6. Write pattern file (all files, comma-separated relative paths)
├── 7. Build CLI arguments
├── 8. Invoke IProcessRunner.RunAsync("dotnet", arguments)
├── 9. Parse stdout/stderr with ESRPCliResultParser
├── 10. Clean up working directory
└── 11. Return success/failure
```

### Configuration

```csharp
public sealed class ESRPCliSigningConfiguration
{
    /// Path to esrpcli.dll.
    public string ESRPCliPath { get; set; }

    /// ESRP gateway URL.
    public string GatewayUrl { get; set; } = "https://api.esrp.microsoft.com/api/v2";

    /// ESRP client ID.
    public string ClientId { get; set; }

    /// AAD tenant ID.
    public string TenantId { get; set; }

    /// Organization name.
    public string Organization { get; set; } = "Microsoft";

    /// Organization info URL.
    public string OrganizationInfoUrl { get; set; } = "https://www.microsoft.com";

    /// Key vault name for PKITA cert.
    public string KeyVaultName { get; set; }

    /// Certificate name in key vault.
    public string CertificateName { get; set; }

    /// Authentication mode.
    public ESRPAuthMode AuthMode { get; set; }

    /// Submission timeout in minutes.
    public int TimeoutInMinutes { get; set; } = 30;

    /// Max batch size per ESRP CLI invocation.
    public int BatchSize { get; set; } = 400;

    /// Temp directory for working files.
    public string TempDirectory { get; set; }

    /// Root directory that file paths are relative to.
    public string RootDirectory { get; set; }

    /// When true, prints submission JSON and arguments but does not invoke ESRP CLI.
    public bool DryRun { get; set; }
}

public enum ESRPAuthMode
{
    FederatedToken,
    Certificate,
}
```

### Process abstraction

```csharp
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken);
}

public sealed class ProcessResult
{
    public int ExitCode { get; }
    public string StandardOutput { get; }
    public string StandardError { get; }
}
```

### Result parsing

```csharp
internal static class ESRPCliResultParser
{
    public static ESRPCliResult Parse(int exitCode, string stdout, string stderr);
}

internal sealed class ESRPCliResult
{
    public bool Success { get; }
    public Guid? OperationId { get; }
    public string? ErrorMessage { get; }
}
```

### Authentication

Authentication is handled via `FederatedToken` and `Certificate` modes. These are encapsulated in argument-building helpers within the provider; the auth complexity is deferred to a future implementation as the current focus is on the submission JSON and dry-run flow.

## Dry-run mode

When `ESRPCliSigningConfiguration.DryRun` is `true`:
1. The provider builds the submission JSON and CLI arguments exactly as it would for a real invocation.
2. It logs the full submission JSON and argument string via `ILogger`.
3. It returns `true` without invoking the ESRP CLI.

This enables validation of certificate rule resolution and submission construction without requiring ESRP credentials or network access.

## Error handling

- **Invalid certificate type**: If any file has a `CertificateIdentifier` that is not `ESRPCertificateIdentifier`, the provider throws `InvalidOperationException`.
- **File path validation**: If a file path is not under `RootDirectory`, `InvalidOperationException` is thrown.
- **Process failure**: Non-zero exit code or `failDoNotRetry` in output causes the method to return `false` after logging details.
- **Working directory cleanup**: Performed in a `finally` block; failures are logged as warnings.
- **Secret safety**: Auth tokens and encryption keys are never logged.
- **Cancellation**: `CancellationToken` is passed through to `IProcessRunner`.

## File layout

New files under `src/Microsoft.DotNet.RecursiveSigning/src/`:

```
Models/
├── ESRPCertificateIdentifier.cs       # Moved from DefaultCertificateRules.cs
├── ESRPCliSigningConfiguration.cs     # Configuration model + ESRPAuthMode enum

Implementation/
├── ESRPCliSigningProvider.cs          # ISigningProvider implementation
├── ESRPCliResultParser.cs             # Stdout/stderr parsing
├── Process/
│   ├── IProcessRunner.cs             # Process execution abstraction
│   └── DefaultProcessRunner.cs       # Process.Start wrapper
```

## Testing strategy

- **JSON construction**: Verify the submission JSON matches expected structure for various cert/file combinations.
- **Grouping**: Files with same cert land in same SignBatch; different certs produce separate batches.
- **Dry-run**: Verify no process is invoked and method returns true.
- **Result parsing**: Success, failure, operation ID extraction, edge cases.
- **Path computation**: Relative paths are correctly derived from RootDirectory.
- **Integration**: Real CLI invocation tested in CI with proper service connections.

# Default Certificate Calculator

This document defines the default certificate calculator architecture with explicit separation of concerns.

## Components
- `DefaultCertificateRules`: immutable mapping object containing:
  - Raw certificate definition JSON keyed by friendly name.
  - File-name rules (`file name -> friendly name`).
  - File-extension rules (`extension -> friendly name`).
- `DefaultCertificateRulesReader`: reads JSON into `DefaultCertificateRules`.
- `DefaultCertificateCalculator`: receives `DefaultCertificateRules` in its constructor and resolves per-file certificate identifiers.
- `ESRPCertificateIdentifier`: concrete `ICertificateIdentifier` carrying friendly name + raw certificate JSON definition.

## JSON format
Top-level object:
- `certificates`: array of certificate definitions.
- `rules`: file-name and extension rule maps.

Certificate shape:
- `friendlyName`: string
- `operations`: array of ESRP-like operations:
  - `keyCode`
  - `operationSetCode`
  - `parameters` (`parameterName`, `parameterValue`)
  - `toolName`
  - `toolVersion`

Rule shape:
- `fileNameMappings`: map of exact file name to certificate friendly name.
- `fileExtensionMappings`: map of extension to certificate friendly name.

## Resolution order
1. Exact file-name mapping.
2. Extension mapping.
3. No match => do not sign (`null` certificate identifier).

If a rule maps to an undefined friendly name, the calculator throws to surface bad configuration.



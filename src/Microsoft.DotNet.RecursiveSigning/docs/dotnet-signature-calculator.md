# .NET Signature Calculator (certificate resolution rules)

This document defines a fresh certificate-resolution algorithm for RecursiveSigning that applies the same core concepts as the existing .NET signing system.

## Inputs
For each file being evaluated:
- File path and filename
- File content hash (or equivalent “is empty” determination)
- Optional parent-container context (only to indicate the file is nested)

The calculator also depends on configuration rule sets:
- **Extension rules**: map file extensions (including multi-part extensions) to a default certificate.
- **Strong-name rules**: map managed assembly identity (public key token, optionally target framework) to a certificate.
- **Explicit file overrides**: map specific filenames (optionally with additional identity qualifiers) to a certificate and flags.
- **Additional certificate metadata**: per-certificate settings such as dual-sign allowance and mac notarization behavior.

## Tests used during evaluation
The calculator may inspect a file to determine:
- Whether the file is empty.
- Whether the file is a Windows PE/COFF binary.
  - If it is a PE file: whether it is managed, its public key token, its target framework (when available), and whether it is crossgen/R2R.
  - Whether it is already Authenticode signed.
  - Whether it is already strong-name signed (for managed binaries).
- For non-PE files: whether the file is one of the known signable package/script formats, and whether it is already signed for that format.
- An “executable type” classification (used only to refine explicit file overrides).

## Output
The result is a resolved signing decision containing:
- The certificate identifier / signing operation to use (or “ignore”).
- Whether the file should be unpacked when it is a container.
- Status flags such as “already signed” and “already strong-name signed”.
- Optional mac notarization instructions (when configured).

## Rule evaluation order (certificate selection precedence)
Certificate resolution follows this precedence:

1. **Explicit file overrides** can override certificates chosen by other rule families.
2. **Strong-name rules** override extension rules for managed binaries.
3. **Extension rules** provide the baseline fallback mapping.

After the certificate is resolved, **additional certificate metadata** is applied to modify behavior (dual-signing, notarization, detached signatures, etc.).

## Detailed algorithm

### 1) Empty files are never signed
If the file is empty, the file is ignored (no signing is attempted).

### 2) Determine the effective extension
When selecting an extension rule, treat configured multi-part extensions as more specific than simple extensions by choosing the **longest matching suffix**.

### 3) Start with extension-based certificate (baseline)
If an extension rule exists for the effective extension, select its default certificate.
- If multiple entries exist in configuration, selection is deterministic.

### 4) If the file is a managed binary, apply strong-name certificate rules
If the file is a managed binary and strong-name rules contain an entry for its public key token:
- Select the configured certificate for that public key token.
- If the binary is crossgen/R2R such that strong-name signing is not applicable, do not schedule strong-name signing; keep only the certificate selection for Authenticode.
- If the binary is already strong-name signed, record that status so strong-name re-signing is not attempted.

### 5) Apply explicit file overrides (most specific match wins)
If explicit override rules exist for the file, apply the most specific match first. Conceptually, specificity is:
1. Filename + strong-name identity + target framework
2. Filename + strong-name identity
3. Filename + executable type classification
4. Filename only

### 6) Resolve “do not unpack”
If an explicit override provides a “do not unpack” setting, it takes precedence; otherwise the extension/strong-name rule’s default applies.

### 7) Explicit ignore overrides everything
If an explicit override indicates the file should not be signed, the file is ignored regardless of any other rule matches.

### 8) Explicit certificate override
If an explicit override specifies a certificate, it replaces the certificate selected by extension/strong-name rules.

### 9) Apply additional certificate metadata
If a certificate has been selected:
- Apply additional per-certificate metadata (for example, whether dual-signing is allowed, and whether mac signing is split into a sign step plus notarization).

### 10) Configuration-driven “skip signing” scenarios
Some certificate identifiers may be used as sentinels to indicate a breaking policy change (for example, “no longer signed by default”). In these cases the file is ignored and a warning may be produced.

### 11) Already-signed behavior (dual-signing)
If the file is already Authenticode signed:
- If dual-signing is not allowed for the selected certificate, do not re-sign the file.
- If dual-signing is allowed, signing may proceed (e.g., to add an additional signature).

### 12) Mac notarization behavior
If the selected certificate configuration indicates a split sign/notarize workflow:
- Replace the certificate with the designated “mac signing” operation.
- Attach a notarization instruction using the configured notarization app name.

### 13) Post-processing validations and modifiers
Before returning, the calculator may:
- Check for mismatches between “first-party vs third-party” expectations and the selected certificate (warning/message only; does not change the selected certificate).
- Switch to detached signatures when required by the selected certificate/file type.

### 14) Missing mapping behavior
If no rule produces a certificate mapping:
- If the file type is considered signable by policy, a configuration error is recorded (a certificate mapping is required).
- Otherwise, the file is ignored as non-signable.

## Notes / non-goals
- This spec describes certificate resolution and related signing-operation selection; it does not specify container traversal or signing scheduling.

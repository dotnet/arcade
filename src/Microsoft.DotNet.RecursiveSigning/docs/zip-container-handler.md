# Zip container handler

## Overview
The zip container handler implements `IContainerHandler` for ZIP-based archives (e.g. `.zip`, `.nupkg`, `.vsix`). It exposes archive entries as `ContainerEntry` instances, and can rewrite the container by copying original entries and replacing entries marked as updated.

This handler is intended to support the recursive signing workflow by:
- Enumerating nested files in a container.
- Providing stable `RelativePath` values for nodes in the signing graph.
- Repacking the container after nested signable files have been updated.

## Formats and identification
- `CanHandle(string filePath)` returns `true` for known ZIP extensions (`.zip`, `.nupkg`, `.vsix`) using case-insensitive comparison.
- The handler does not attempt content-based detection.

## Reading entries
### Filtering
- Directory entries (ZIP entries whose `FullName` ends with `/`) are ignored.
- `RelativePath` is the ZIP entry `FullName` exactly as stored in the archive.

### Stream ownership
- `ReadEntriesAsync` produces a `ContainerEntry` where `ContainerEntry.ContentStream` is a *standalone* in-memory stream containing the entry bytes.
- The caller owns each returned `ContainerEntry` and is responsible for disposing it.

### Stream validity
- The returned `ContentStream` does not depend on the underlying `ZipArchive` remaining open. The archive can be disposed immediately after the entry is read.
- The stream is positioned at the beginning when returned.

### Metadata surfaced on entries
- `ContainerEntry.Length` is set to the entry length.
- `ContainerEntry.ContentHash` is set to SHA-256 of the entry bytes.

## Writing containers
### High-level behavior
`WriteContainerAsync` rewrites the container at `containerPath`.
- If any entry is updated (`IsUpdated == true`), the container is fully rewritten.
- For each entry:
  - If `IsUpdated == true` and `UpdatedContentPath` is set, data is read from the file at `UpdatedContentPath`.
  - Otherwise, data is read from `ContentStream`.
- Entries are written into a new ZIP archive, and then the original file is replaced atomically (best effort) by moving the temp file into place.

### Stream ownership and validity
- The caller retains ownership of `ContainerEntry.ContentStream` and the handler must not dispose these streams.
- The handler may seek entry streams to the beginning when reading; callers should not assume the position is preserved.

### Compression and timestamps
- The handler uses the platform `System.IO.Compression.ZipArchive` implementation.
- Compression level defaults to the framework default.
- Existing per-entry timestamps/attributes are not preserved (future enhancement).

## Error handling
- Invalid paths and missing updated content paths cause `ArgumentException`/`FileNotFoundException` where appropriate.
- Corrupt ZIPs surface as exceptions from `System.IO.Compression`.

## Testing strategy
Unit tests use only in-memory data:
- A helper creates a ZIP payload in a `MemoryStream` and writes it to a temp file for handler input.
- Tests validate:
  - `CanHandle` extension matching.
  - Entry enumeration returns expected `RelativePath` and bytes.
  - Content hash and length are populated.
  - `WriteContainerAsync` replaces updated entries and does not dispose input streams.

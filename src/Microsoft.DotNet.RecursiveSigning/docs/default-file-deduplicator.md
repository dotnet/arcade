# Default File Deduplicator

The default implementation of `IFileDeduplicator` (referred to here as the DefaultFileDeduplicator) manages file deduplication across the recursive signing workflow by tracking unique files and reusing signed versions when identical content is encountered.

## Responsibilities
- Track all files discovered during the signing workflow by their unique content key (hash + filename combination).
- Maintain a registry of signed file versions so that duplicate content can reuse the same signed artifact.
- Provide thread-safe operations for concurrent file registration and lookup during parallel signing operations.

## Content Key Definition
Files are uniquely identified by a `FileContentKey` which combines:
1. **Content Hash**: SHA-256 hash of the file's binary content
2. **File Name**: The base filename (without path)

This means two files are considered duplicates ONLY if:
- They have identical binary content (same hash), AND
- They have the same filename

This design ensures that files with the same content but different names (which may have different semantic meanings in different contexts) are NOT deduplicated, while truly identical files across different containers or directory locations ARE deduplicated.

## Deduplication Workflow

### Registration Phase (Discovery)
1. As each file is analyzed during discovery, the orchestrator calls `TryRegisterFile` with its `FileMetadata`.
2. If the content key has not been seen before, `TryRegisterFile` returns `true`.
3. If the content key is already registered, `TryRegisterFile` returns `false` and provides the first-registered file path for that content key.

### Signing Phase
1. Before signing a file, the orchestrator checks if a signed version already exists via `TryGetSignedVersion`.
2. If a signed version exists:
   - The existing signed file path is returned
   - The file is marked as "already signed" (WasAlreadySigned = true)
   - No actual signing operation is performed
3. If no signed version exists:
   - The file is sent to the signing provider
   - After successful signing, the signed file path is registered via `RegisterSignedFile`

### Query Phase
1. The `GetUniqueKeys` method returns all unique content keys encountered during the workflow.

## Thread Safety
The DefaultFileDeduplicator uses `ConcurrentDictionary` and `ConcurrentBag` to ensure thread-safe operations:
- Multiple threads can register files simultaneously during parallel discovery
- Signed file registration is atomic
- Lookups for existing signed versions are safe during concurrent signing rounds

## Storage Strategy
Two concurrent dictionaries maintain the deduplication state:
1. **First-Seen Path Registry**: `ConcurrentDictionary<FileContentKey, string>`
   - Maps each unique content key to the first file path observed for that content
   - Enables duplicate detection and reuse of the original on-disk path when a duplicate is encountered
2. **Signed Version Registry**: `ConcurrentDictionary<FileContentKey, string>`
   - Maps each unique content key to the path of its signed version
   - Enables quick lookup during signing to reuse existing signed files

## Deduplication Scenarios

### Scenario 1: Identical Files in Different Containers
```
package1.nupkg
  ├─ lib/netstandard2.0/Common.dll  (hash: ABC123, size: 50KB)
  └─ ...

package2.nupkg
  ├─ lib/netstandard2.0/Common.dll  (hash: ABC123, size: 50KB)
  └─ ...
```
**Outcome**: Common.dll is signed once, and the signed version is reused in both packages.

### Scenario 2: Same Content, Different Names
```
package.nupkg
  ├─ tools/tool-v1.exe  (hash: DEF456)
  └─ tools/tool-v2.exe  (hash: DEF456, renamed copy)
```
**Outcome**: Both files are signed separately because they have different filenames, even though content is identical. This is intentional because filename differences may have semantic significance.

### Scenario 3: Nested Containers with Common Dependencies
```
outer.nupkg
  ├─ inner1.nupkg
  │   └─ lib/Common.dll  (hash: ABC123)
  └─ inner2.nupkg
      └─ lib/Common.dll  (hash: ABC123)
```
**Outcome**: Common.dll is signed once during the first inner package processing, and the signed version is reused for the second inner package.

## Performance Characteristics
- **Registration**: O(1) average case for recording the first-seen path
- **Signed Version Lookup**: O(1) average case for checking if signed version exists
 - **Memory**: Stores the first-seen file path per unique content key; signed version paths are small (one per unique content key)

## Design Rationale

### Why Include Filename in Content Key?
Including the filename ensures that semantically different files are not incorrectly deduplicated. For example:
- `config.json` vs `config.template.json` might have identical content temporarily but serve different purposes
- `assembly.dll` vs `assembly.resources.dll` must not be confused even if they temporarily have the same content
- Executables with different names (e.g., `mytool.exe` vs `mytool-debug.exe`) should be signed with appropriate certificates that might differ

### Why Not Use File Path?
The full file path is NOT included in the content key because:
- Files in different containers or directories with the same name and content ARE true duplicates
- Container repacking changes paths but not semantic identity
- Deduplication across container boundaries is a key performance optimization

## Limitations and Future Enhancements
- **Current Limitation**: The deduplicator does not persist state across signing runs. Each run starts fresh.
- **Future Enhancement**: Could add persistent caching to reuse signed files across multiple signing pipeline executions.
- **Current Limitation**: No automatic cleanup of temporary signed file storage during the run.
- **Future Enhancement**: Could implement reference counting to delete signed versions once all containers using them are repacked.

## Integration with Signing Workflow
The DefaultFileDeduplicator is registered as a singleton service in the dependency injection container:
```csharp
services.AddSingleton<IFileDeduplicator, DefaultFileDeduplicator>();
```

This ensures a single instance tracks all files throughout the entire signing session, enabling global deduplication across all input artifacts and their nested contents.

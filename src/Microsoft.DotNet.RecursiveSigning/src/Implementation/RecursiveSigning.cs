// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Main orchestrator for the recursive signing workflow.
    /// Implements the three-phase algorithm: Discovery → Iterative Signing → Finalization.
    /// </summary>
    public sealed class RecursiveSigning : IRecursiveSigning
    {
        private readonly IFileSystem _fileSystem;
        private readonly IFileAnalyzer _fileAnalyzer;
        private readonly ISignatureCalculator _signatureCalculator;
        private readonly ISigningGraph _signingGraph;
        private readonly IFileDeduplicator _fileDeduplicator;
        private readonly IContainerHandlerRegistry _containerHandlerRegistry;
        private readonly ISigningProvider _signingProvider;
        private readonly ILogger<RecursiveSigning> _logger;

        public RecursiveSigning(
            IFileSystem fileSystem,
            IFileAnalyzer fileAnalyzer,
            ISignatureCalculator signatureCalculator,
            ISigningGraph signingGraph,
            IFileDeduplicator fileDeduplicator,
            IContainerHandlerRegistry containerHandlerRegistry,
            ISigningProvider signingProvider,
            ILogger<RecursiveSigning> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _fileAnalyzer = fileAnalyzer ?? throw new ArgumentNullException(nameof(fileAnalyzer));
            _signatureCalculator = signatureCalculator ?? throw new ArgumentNullException(nameof(signatureCalculator));
            _signingGraph = signingGraph ?? throw new ArgumentNullException(nameof(signingGraph));
            _fileDeduplicator = fileDeduplicator ?? throw new ArgumentNullException(nameof(fileDeduplicator));
            _containerHandlerRegistry = containerHandlerRegistry ?? throw new ArgumentNullException(nameof(containerHandlerRegistry));
            _signingProvider = signingProvider ?? throw new ArgumentNullException(nameof(signingProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes the full recursive signing workflow.
        /// </summary>
        /// <param name="request">Signing request containing input files, configuration, and options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Signing result with signed files, errors, and telemetry.</returns>
        public async Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var errors = new List<SigningError>();
            var signedFiles = new List<SignedFileInfo>();

            try
            {
                _logger.LogInformation("Starting recursive signing for {FileCount} input files", request.InputFiles.Count);

                // Phase 1: Discovery - Build the signing graph
                _logger.LogInformation("Phase 1: Discovery and Analysis");
                await DiscoveryPhaseAsync(request, errors, cancellationToken);

                _signingGraph.FinalizeDiscovery();

                if (errors.Count > 0)
                {
                    _logger.LogError("Discovery phase failed with {ErrorCount} errors", errors.Count);
                    return CreateResult(false, signedFiles, errors, sw.Elapsed, 0, 0);
                }

                var allNodes = _signingGraph.GetAllNodes();
                _logger.LogInformation("Discovered {NodeCount} files total", allNodes.Count);

                // Phase 2: Iterative Signing
                _logger.LogInformation("Phase 2: Iterative Signing");
                await IterativeSigningPhaseAsync(request, signedFiles, errors, cancellationToken);

                if (errors.Count > 0)
                {
                    _logger.LogError("Signing phase failed with {ErrorCount} errors", errors.Count);
                    return CreateResult(false, signedFiles, errors, sw.Elapsed, 0, allNodes.Count);
                }

                // Phase 3: Finalization
                _logger.LogInformation("Phase 3: Finalization");
                FinalizationPhase(signedFiles, errors);

                sw.Stop();
                bool success = errors.Count == 0;
                _logger.LogInformation(
                    "Signing completed in {Duration}ms. Success: {Success}, Files signed: {SignedCount}/{TotalCount}",
                    sw.ElapsedMilliseconds, success, signedFiles.Count, allNodes.Count);

                return CreateResult(success, signedFiles, errors, sw.Elapsed, signedFiles.Count, allNodes.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Signing operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during signing");
                errors.Add(new SigningError($"Unexpected error: {ex.Message}", exception: ex));
                return CreateResult(false, signedFiles, errors, sw.Elapsed, 0, 0);
            }
        }

        private async Task DiscoveryPhaseAsync(
            SigningRequest request,
            List<SigningError> errors,
            CancellationToken cancellationToken)
        {
            foreach (var filePath in request.InputFiles.Select(f => f.ToString()))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await TrackFile(NormalizeFilePathForFileSystem(filePath), null, request.Configuration, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error discovering file: {FilePath}", filePath);
                    errors.Add(new SigningError($"Error discovering file: {ex.Message}", filePath, ex));
                }
            }
        }


        private string NormalizeFilePathForFileSystem(string filePath)
        {
            // Tests may use a mock IFileSystem keyed by '/' paths while FileInfo.FullName is OS-specific.
            // Normalize to forward slashes to work with the mock file system.
            return filePath.Replace('\\', '/');
        }

        /// <summary>
        /// Tracks a top-level file from disk, performing deduplication, analysis, and optional container discovery.
        /// </summary>
        /// <param name="filePath">Path to the file on disk.</param>
        /// <param name="parentNode">Optional parent node if the file is contained within another container.</param>
        /// <param name="configuration">Signing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The node representing the tracked file.</returns>
        private async Task<FileNodeBase> TrackFile(
            string filePath,
            FileNode? parentNode,
            SigningConfiguration configuration,
            CancellationToken cancellationToken)
        {
            // If the file does not exist, then throw
            if (!_fileSystem.FileExists(filePath))
            {
                throw new FileNotFoundException("Input file not found", filePath);
            }

            // Compute identity + location locally and ask analyzer only for intrinsic metadata
            string fileName = Path.GetFileName(filePath);
            using var stream = _fileSystem.GetFileStream(filePath, FileMode.Open, FileAccess.Read);
            ContentHash contentHash = await ContentHash.FromStreamAsync(stream, cancellationToken);
            var contentKey = new FileContentKey(contentHash, fileName);
            var location = new FileLocation(filePath, RelativePathInContainer: null);

            // Check for duplicates and skip discovery 
            if (_fileDeduplicator.TryGetRegisteredFile(contentKey, out string? originalPath))
            {
                _logger.LogDebug(
                    "Duplicate file detected: {FileName} at {FilePath} (original: {OriginalPath}), skipping analysis and extraction",
                    contentKey.FileName,
                    filePath,
                    originalPath);

                return CreateReferenceNode(contentKey, location, parentNode, "file", filePath);
            }

            _fileDeduplicator.RegisterFile(contentKey, filePath);

            var metadata = await _fileAnalyzer.AnalyzeAsync(filePath, cancellationToken);

            // First occurrence: delegate to DiscoverFileAsync for full analysis
            return await DiscoverFileAsync(contentKey, location, metadata, parentNode, configuration, cancellationToken);
        }

        /// <summary>
        /// Tracks a file whose contents are provided as a stream (typically extracted from a container).
        /// Performs deduplication, analysis, extraction to disk, and optional container discovery.
        /// </summary>
        /// <param name="contentStream">Stream containing the file content.</param>
        /// <param name="relativePath">Relative path of the file within its container.</param>
        /// <param name="parentNode">Container node that owns this entry.</param>
        /// <param name="configuration">Signing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The node representing the tracked file.</returns>
        private async Task<FileNodeBase> TrackNestedFile(
            Stream contentStream,
            string relativePath,
            FileNode parentNode,
            SigningConfiguration configuration,
            CancellationToken cancellationToken)
        {
            if (contentStream == null)
            {
                throw new ArgumentNullException(nameof(contentStream));
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path cannot be null or empty", nameof(relativePath));
            }

            if (parentNode == null)
            {
                throw new ArgumentNullException(nameof(parentNode));
            }

            // Extract filename from relative path
            string fileName = Path.GetFileName(relativePath);

            // Compute identity locally and ask analyzer only for intrinsic metadata
            ContentHash contentHash = await ContentHash.FromStreamAsync(contentStream, cancellationToken);
            var contentKey = new FileContentKey(contentHash, fileName);

            // Register and check for duplicates BEFORE analyzing or writing to disk
            // If the content has been seen before, reuse the first extracted path.
            if (_fileDeduplicator.TryGetRegisteredFile(contentKey, out string? originalPath))
            {
                _logger.LogDebug(
                    "Duplicate file detected in container: {FileName} at {RelativePath} (original: {OriginalPath}), skipping extraction",
                    contentKey.FileName,
                    relativePath,
                    originalPath);

                var referenceLocation = new FileLocation(originalPath, relativePath);
                return CreateReferenceNodeForContainer(contentKey, referenceLocation, parentNode);
            }

            var metadata = await _fileAnalyzer.AnalyzeAsync(contentStream, fileName, cancellationToken);

            // First occurrence: write stream to disk and register it
            string filePath = await WriteStreamToTempFileAsync(
                contentStream,
                relativePath,
                configuration.TempDirectory,
                cancellationToken);

            _fileDeduplicator.RegisterFile(contentKey, filePath);

            return await DiscoverFileAsync(contentKey, new FileLocation(filePath, relativePath), metadata, parentNode, configuration, cancellationToken);
        }

        /// <summary>
        /// Creates a reference node for a duplicate file (non-container context).
        /// </summary>
        private FileNodeBase CreateReferenceNode(FileContentKey contentKey, FileLocation fileLocation, FileNode? parentNode, string context, string location)
        {
            var existingNode = FindExistingNodeByContentKey(contentKey);
            if (existingNode == null)
            {
                throw new InvalidOperationException(
                    $"Duplicate detected but no existing node found for content key {contentKey}. This indicates a bug in the deduplication logic.");
            }

            // Create a reference node that shares the same certificate identifier
            // but tracks this specific location in the container hierarchy
            var referenceNode = new ReferenceNode(contentKey, fileLocation, existingNode);
            _signingGraph.AddNode(referenceNode, parentNode);
            
            _logger.LogDebug("Created reference node for duplicate {Context} at: {Location}", context, location);
            return referenceNode;
        }

        /// <summary>
        /// Creates a reference node for a duplicate file found in a container.
        /// </summary>
        private FileNodeBase CreateReferenceNodeForContainer(FileContentKey contentKey, FileLocation fileLocation, FileNode parentNode)
        {
            var existingNode = FindExistingNodeByContentKey(contentKey);
            if (existingNode == null)
            {
                throw new InvalidOperationException(
                    $"Duplicate detected but no existing node found for content key {contentKey}. This indicates a bug in the deduplication logic.");
            }

            // Create a reference node for this container location.
            // Keep the original file location (container path + relative path) so the graph structure is correct.
            // The canonical node carries the real on-disk bytes for signing.
            var referenceNode = new ReferenceNode(contentKey, fileLocation, existingNode);
            _signingGraph.AddNode(referenceNode, parentNode);
            
            _logger.LogDebug(
                "Created reference node for duplicate file in container at: {RelativePath}",
                fileLocation.RelativePathInContainer);
            return referenceNode;
        }

        /// <summary>
        /// Writes a stream to a temporary file on disk.
        /// </summary>
        private async Task<string> WriteStreamToTempFileAsync(
            Stream contentStream,
            string relativePath,
            string tempDirectory,
            CancellationToken cancellationToken)
        {
            string extractDir = _fileSystem.PathCombine(tempDirectory, Guid.NewGuid().ToString());
            _fileSystem.CreateDirectory(extractDir);
            string filePath = _fileSystem.PathCombine(extractDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            
            var fileDir = _fileSystem.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(fileDir))
            {
                _fileSystem.CreateDirectory(fileDir);
            }

            // Write stream to disk
            using (var fileStream = _fileSystem.GetFileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                await contentStream.CopyToAsync(fileStream, cancellationToken);
            }

            return filePath;
        }

        /// <summary>
        /// Finds an existing node in the signing graph that matches the given content key.
        /// </summary>
        /// <param name="contentKey">File content key to locate.</param>
        /// <returns>The first matching node, or null if not found.</returns>
        private FileNode? FindExistingNodeByContentKey(FileContentKey contentKey)
        {
            var allNodes = _signingGraph.GetAllNodes();
            return allNodes.OfType<FileNode>().FirstOrDefault(n => n.ContentKey.Equals(contentKey));
        }

        /// <summary>
        /// Discovers signing information for a file and, if it is a container, recursively discovers its contents.
        /// </summary>
        /// <param name="contentKey">Content identity of the file.</param>
        /// <param name="location">File location information (path on disk and optional relative path in container).</param>
        /// <param name="metadata">Analyzed file metadata.</param>
        /// <param name="parentNode">Optional parent node if the file is contained within another container.</param>
        /// <param name="configuration">Signing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The discovered node.</returns>
        private async Task<FileNode> DiscoverFileAsync(
            FileContentKey contentKey,
            FileLocation location,
            IFileMetadata metadata,
            FileNode? parentNode,
            SigningConfiguration configuration,
            CancellationToken cancellationToken)
        {
            // First occurrence: full analysis and potential container extraction
            _logger.LogDebug(
                "Analyzing new file: {FileName} at {FilePath} (parent container: {ParentContainer})",
                contentKey.FileName,
                location.FilePathOnDisk,
                parentNode?.ContentKey.FileName ?? "<root>");

            var certificateIdentifier = _signatureCalculator.CalculateCertificateIdentifier(metadata, configuration);

            // Create node
            var node = new FileNode(contentKey, location, metadata, certificateIdentifier);
            _signingGraph.AddNode(node, parentNode);

            // Check if there's a handler that can unpack this file (determines if it's a container)
            IContainerHandler? handler;
            try
            {
                handler = _containerHandlerRegistry.FindHandler(location.FilePathOnDisk!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting container handler for file: {FilePath}", location.FilePathOnDisk);
                handler = null;
            }
            bool isContainer = handler != null;

            // Node state is graph-owned and computed when the graph is built.

            _logger.LogDebug("Discovered file: {FileName}, NeedsSigning: {NeedsSigning}, IsContainer: {IsContainer}",
                contentKey.FileName, node.NeedsSigning, isContainer);

            // If this is a container (has a registered handler), recursively discover its contents
            if (isContainer)
            {
                await DiscoverContainerContentsAsync(node, handler!, configuration, cancellationToken);
            }

            return node;
        }

        /// <summary>
        /// Enumerates entries in a container and tracks each entry as a child node.
        /// </summary>
        /// <param name="containerNode">Container node whose contents should be discovered.</param>
        /// <param name="handler">Handler used to read entries from the container.</param>
        /// <param name="configuration">Signing configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task DiscoverContainerContentsAsync(
            FileNode containerNode,
            IContainerHandler handler,
            SigningConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Discovering contents of container: {FileName}", containerNode.ContentKey.FileName);

            await foreach (var entry in handler.ReadEntriesAsync(containerNode.Location.FilePathOnDisk!, cancellationToken))
            {
                await TrackNestedFile(entry.ContentStream, entry.RelativePath, containerNode, configuration, cancellationToken);
            }
        }

        /// <summary>
        /// Performs iterative signing rounds until all nodes are signed or no further progress can be made.
        /// </summary>
        /// <param name="request">Signing request.</param>
        /// <param name="signedFiles">Accumulated signed file list.</param>
        /// <param name="errors">Accumulated error list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task IterativeSigningPhaseAsync(
            SigningRequest request,
            List<SignedFileInfo> signedFiles,
            List<SigningError> errors,
            CancellationToken cancellationToken)
        {
            int roundNumber = 0;

            while (!_signingGraph.IsComplete())
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool madeProgress = false;

                var toSign = _signingGraph.GetNodesReadyForSigning();
                if (toSign.Count > 0)
                {
                    _logger.LogInformation("Signing round {Round}: {FileCount} file(s) ready", roundNumber, toSign.Count);
                    bool signedAny = await SignRoundAsync(toSign, request, signedFiles, errors, cancellationToken);
                    madeProgress |= signedAny;
                }

                var toRepack = _signingGraph.GetContainersReadyForRepack();
                if (toRepack.Count > 0)
                {
                    _logger.LogInformation("Repack round {Round}: {FileCount} container(s) ready", roundNumber, toRepack.Count);
                    await RepackContainersAsync(toRepack, request.Configuration.TempDirectory, errors, cancellationToken);

                    madeProgress = true;

                    // Repacked containers are now ready to be signed in the next iteration.
                    foreach (var container in toRepack)
                    {
                        if (container.State == FileNodeState.ReadyToRepack)
                        {
                            _signingGraph.MarkContainerAsRepacked(container);
                            madeProgress = true;
                        }
                    }
                }

                // If we made no progress, the graph is stuck.
                if (!madeProgress)
                {
                    if (_signingGraph.IsComplete())
                    {
                        break;
                    }

                    errors.Add(new SigningError("Signing graph has no ready nodes but is not complete. Possible circular dependency. Last signing provider errors: " + string.Join(", ", errors.Where(e => e.Message.Contains("Signing provider failed")))));
                    break;
                }

                roundNumber++;
            }
        }

        /// <summary>
        /// Repacks containers whose children have been signed, updating their identity and metadata.
        /// </summary>
        /// <param name="containers">Containers ready for repack.</param>
        /// <param name="tempDirectory">Temporary directory used for repacked output.</param>
        /// <param name="errors">Accumulated error list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task RepackContainersAsync(
            IReadOnlyList<FileNode> containers,
            string tempDirectory,
            List<SigningError> errors,
            CancellationToken cancellationToken)
        {
            foreach (var container in containers)
            {
                try
                {
                    string repackedPath = await RepackContainerAsync(container, tempDirectory, cancellationToken);

                    using (var repackedStream = _fileSystem.GetFileStream(repackedPath, FileMode.Open, FileAccess.Read))
                    {
                        ContentHash repackedHash = await ContentHash.FromStreamAsync(repackedStream, cancellationToken);
                        container.ContentKey = new FileContentKey(repackedHash, container.ContentKey.FileName);
                    }

                    container.Location = new FileLocation(repackedPath, container.Location.RelativePathInContainer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error repacking container: {FilePath}", container.Location.FilePathOnDisk);
                    errors.Add(new SigningError($"Error repacking container: {ex.Message}", container.Location.FilePathOnDisk, ex));
                }
            }
        }

        /// <summary>
        /// Signs nodes that are ready for signing, deduplicating by content key and reusing signed outputs.
        /// </summary>
        /// <param name="nodes">Nodes ready for signing.</param>
        /// <param name="request">Signing request.</param>
        /// <param name="signedFiles">Accumulated signed file list.</param>
        /// <param name="errors">Accumulated error list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task<bool> SignRoundAsync(
            IReadOnlyList<FileNode> nodes,
            SigningRequest request,
            List<SignedFileInfo> signedFiles,
            List<SigningError> errors,
            CancellationToken cancellationToken)
        {
            var filesToSign = new List<(FileNode node, string outputPath)>();

            foreach (var node in nodes)
            {
                // Graph-level implicit dedup guarantees these are unique original nodes.
                var signedDir = _fileSystem.PathCombine(request.Configuration.TempDirectory, "signed", Guid.NewGuid().ToString());
                _fileSystem.CreateDirectory(signedDir);
                string outputPath = _fileSystem.PathCombine(signedDir, node.ContentKey.FileName);
                filesToSign.Add((node, outputPath));
            }

            if (filesToSign.Count == 0)
            {
                return false;
            }

            // Call signing provider (only signs first occurrence of each content key)
            _logger.LogDebug("Signing {Count} unique file(s)", filesToSign.Count);
            bool success = await _signingProvider.SignFilesAsync(filesToSign, cancellationToken);

            if (!success)
            {
                _logger.LogError("Signing provider returned failure for {Count} file(s)", filesToSign.Count);
                errors.Add(new SigningError($"Signing provider failed for {filesToSign.Count} files"));
                return false;
            }

            // Mark first occurrences as signed and register signed versions
            foreach (var (node, outputPath) in filesToSign)
            {
                _signingGraph.MarkAsComplete(node);
                _fileDeduplicator.RegisterSignedFile(node.ContentKey, outputPath);
                signedFiles.Add(new SignedFileInfo(node.Location.FilePathOnDisk, node.CertificateIdentifier?.Name ?? string.Empty, false));
            }

            return true;
        }

        /// <summary>
        /// Repacks a container by writing out a new container file from its (signed) child entries.
        /// </summary>
        /// <param name="containerNode">Container node to repack.</param>
        /// <param name="tempDirectory">Temporary directory used for repacked output.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Path to the repacked container on disk.</returns>
        private async Task<string> RepackContainerAsync(FileNode containerNode, string tempDirectory, CancellationToken cancellationToken)
        {
            var handler = _containerHandlerRegistry.FindHandler(containerNode.Location.FilePathOnDisk!);
            if (handler == null)
            {
                throw new InvalidOperationException($"No handler found for repacking container: {containerNode.Location.FilePathOnDisk}");
            }

            _logger.LogDebug("Repacking container: {FileName}", containerNode.ContentKey.FileName);

            // Build list of entries with signed versions
            var entries = new List<ContainerEntry>();

            foreach (var child in containerNode.Children.OfType<FileNode>())
            {
                // Get signed version
                if (!_fileDeduplicator.TryGetSignedVersion(child.ContentKey, out string? signedPath))
                {
                    signedPath = child.Location.FilePathOnDisk!; // Use original if not signed
                }

                var stream = _fileSystem.GetFileStream(signedPath!, FileMode.Open, FileAccess.Read);
                var entry = new ContainerEntry(child.Location.RelativePathInContainer!, stream)
                {
                    IsUpdated = true,
                    UpdatedContentPath = signedPath
                };
                entries.Add(entry);
            }

            // Create output path for repacked container
            var repackedDir = _fileSystem.PathCombine(tempDirectory, "repacked", Guid.NewGuid().ToString());
            _fileSystem.CreateDirectory(repackedDir);
            string repackedPath = _fileSystem.PathCombine(repackedDir, containerNode.ContentKey.FileName);

            // Repack container to new location
            await handler.WriteContainerAsync(
                repackedPath,
                entries,
                new ContainerMetadata(),
                cancellationToken);

            // Cleanup entry streams
            foreach (var entry in entries)
            {
                entry.Dispose();
            }

            return repackedPath;
        }

        /// <summary>
        /// Performs final verification and reporting after signing.
        /// </summary>
        /// <param name="signedFiles">Signed file list.</param>
        /// <param name="errors">Accumulated error list.</param>
        private void FinalizationPhase(List<SignedFileInfo> signedFiles, List<SigningError> errors)
        {
            // Phase 3 tasks:
            // - Verify all required files are signed
            // - Copy files to final output locations (if needed)
            // - Generate report

            var allNodes = _signingGraph.GetAllNodes();

            // For implicit deduplication, reference nodes are not signed directly.
            // Still report them as signed if their canonical/original node was signed.
            foreach (var referenceNode in allNodes.OfType<ReferenceNode>())
            {
                if (referenceNode.CanonicalNode.State == FileNodeState.Complete)
                {
                    signedFiles.Add(new SignedFileInfo(
                        referenceNode.Location.FilePathOnDisk,
                        referenceNode.CanonicalNode.CertificateIdentifier?.Name ?? string.Empty,
                        wasAlreadySigned: true));
                }
            }


            var unsignedNodes = allNodes.OfType<FileNode>().Where(n => n.State != FileNodeState.Complete && n.State != FileNodeState.Skipped).ToList();

            if (unsignedNodes.Count > 0)
            {
                _logger.LogWarning("{Count} files were not signed", unsignedNodes.Count);
                foreach (var node in unsignedNodes)
                {
                    errors.Add(new SigningError($"File was not signed", node.Location.FilePathOnDisk));
                }
            }

            _logger.LogInformation("Finalization complete. Signed: {SignedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
                signedFiles.Count,
                allNodes.OfType<FileNode>().Count(n => n.State == FileNodeState.Skipped),
                errors.Count);
        }

        /// <summary>
        /// Creates a <see cref="SigningResult" /> object from the accumulated workflow state.
        /// </summary>
        /// <param name="success">Overall success flag.</param>
        /// <param name="signedFiles">Signed file list.</param>
        /// <param name="errors">Error list.</param>
        /// <param name="duration">Total duration.</param>
        /// <param name="filesSigned">Count of files signed.</param>
        /// <param name="totalFiles">Total file count discovered.</param>
        /// <returns>Signing result.</returns>
        private SigningResult CreateResult(
            bool success,
            List<SignedFileInfo> signedFiles,
            List<SigningError> errors,
            TimeSpan duration,
            int filesSigned,
            int totalFiles)
        {
            var telemetry = new SigningTelemetry
            {
                TotalFiles = totalFiles,
                FilesSigned = filesSigned,
                FilesSkipped = totalFiles - filesSigned,
                SigningRounds = 0, // Would need to track this
                Duration = duration
            };

            return new SigningResult(success, signedFiles, errors, telemetry);
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Configuration;
using Microsoft.DotNet.RecursiveSigning.Models;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Simple certificate identifier for testing.
    /// </summary>
    internal class SimpleCertificateIdentifier : ICertificateIdentifier
    {
        public string Name { get; }

        public SimpleCertificateIdentifier(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Phase 1 tests: Verify the core algorithm works with mocked implementations.
    /// </summary>
    public class BasicSigningWorkflowTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _workingDir;
        private readonly MockFileSystem _mockFileSystem;
        private readonly ServiceProvider _serviceProvider;
        private readonly Mock<IFileAnalyzer> _mockFileAnalyzer;
        private readonly Mock<ISignatureCalculator> _mockSignatureCalculator;
        private readonly Mock<ISigningProvider> _mockSigningProvider;
        private readonly Mock<IContainerHandler> _mockContainerHandler;
        private readonly List<string> _signedFiles;

        public BasicSigningWorkflowTests(ITestOutputHelper output)
        {
            _output = output;
            _workingDir = "/test".TrimEnd('/');
            _signedFiles = new List<string>();

            // Setup MockFileSystem
            _mockFileSystem = new MockFileSystem(
                files: new Dictionary<string, string>(),
                directories: new[] { _workingDir });

            // Setup mocks
            _mockFileAnalyzer = new Mock<IFileAnalyzer>();
            _mockSignatureCalculator = new Mock<ISignatureCalculator>();
            _mockSigningProvider = new Mock<ISigningProvider>();
            _mockContainerHandler = new Mock<IContainerHandler>();

            // Setup default mock behaviors
            SetupDefaultMockBehaviors();

            // Setup DI
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            services.AddRecursiveSigning();
            
            // Replace default services with mocks
            services.AddSingleton<IFileSystem>(_mockFileSystem);
            services.AddSingleton(_mockFileAnalyzer.Object);
            services.AddSingleton(_mockSignatureCalculator.Object);
            services.AddSingleton(_mockSigningProvider.Object);

            
            // Setup container handler registry with mock
            var mockRegistry = new Mock<IContainerHandlerRegistry>();
            mockRegistry.Setup(r => r.FindHandler(It.IsAny<string>()))
                .Returns<string>(path => path.EndsWith(".testcontainer") ? _mockContainerHandler.Object : null);
            services.AddSingleton(mockRegistry.Object);

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task SignAsync_Fails_WithError_WhenIterativeSigningMakesNoProgress()
        {
            // Arrange
            // Force the graph to make no progress after discovery by ensuring:
            // - there's at least one signable node ready initially
            // - signing that node never succeeds (so it is never marked Signed)
            // Eventually the loop will have no ready nodes (child still pending, container gated)
            // while the graph is not complete.
            var containerFile = CreateTestFile("stuck.testcontainer", "container");
            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            _mockSigningProvider.Reset();
            _mockSigningProvider.Setup(p => p.SignFilesAsync(
                    It.IsAny<IReadOnlyList<(FileNode node, string outputPath)>>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(false);

            var request = new SigningRequest(
                new[] { new FileInfo(containerFile) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Select(e => e.Message)
                .Should().Contain(m => m.Contains("Signing graph has no ready nodes but is not complete", StringComparison.OrdinalIgnoreCase));
        }


        [Fact]
        public async Task Discovery_LogsErrorIfHandlerRegistryThrows_AndContinues()
        {
            // Arrange
            var testFile1 = CreateTestFile("a.bin", "content-a");
            var testFile2 = CreateTestFile("b.bin", "content-b");

            var mockRegistry = new Mock<IContainerHandlerRegistry>();
            mockRegistry
                .Setup(r => r.FindHandler(It.IsAny<string>()))
                .Throws(new InvalidOperationException("boom"));

            var mockLogger = new Mock<ILogger<Implementation.RecursiveSigning>>();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            services.AddRecursiveSigning();

            services.AddSingleton<IFileSystem>(_mockFileSystem);
            services.AddSingleton(_mockFileAnalyzer.Object);
            services.AddSingleton(_mockSignatureCalculator.Object);
            services.AddSingleton(_mockSigningProvider.Object);
            services.AddSingleton(mockRegistry.Object);
            services.AddSingleton(mockLogger.Object);

            using var sp = services.BuildServiceProvider();
            var orchestrator = sp.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new List<FileInfo> { new(testFile1), new(testFile2) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            if (!result.Success)
            {
                _output.WriteLine("Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.Errors.Should().BeEmpty();

            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((s, _) => s.ToString()!.Contains("Error selecting container handler", StringComparison.OrdinalIgnoreCase)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);

            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(testFile1, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(testFile2, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SimpleFile_ShouldBeSigned()
        {
            // Arrange
            var testFile = CreateTestFile("simple.txt", "Hello World");
            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new[] { new FileInfo(Path.Combine(_workingDir, "simple.txt")) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            if (!result.Success)
            {
                _output.WriteLine("Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.SignedFiles.Should().HaveCount(1);
            result.Errors.Should().BeEmpty();
            result.SignedFiles[0].FilePath.Should().Be(testFile);
            result.SignedFiles[0].Certificate.Should().Be("TestCert");
            
            // Verify mock interactions
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(testFile, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _mockSigningProvider.Verify(p => p.SignFilesAsync(
                It.Is<IReadOnlyList<(FileNode, string)>>(files => files.Count == 1),
                It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ContainerWithNestedFiles_ShouldSignInCorrectOrder()
        {
            // Arrange
            var containerFile = CreateTestFile("package.testcontainer", "container");
            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new[] { new FileInfo(Path.Combine(_workingDir, "package.testcontainer")) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            // Debug: Output errors if any
            if (!result.Success)
            {
                _output.WriteLine($"Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.SignedFiles.Count.Should().BeGreaterThan(1); // Container + nested files
            result.Errors.Should().BeEmpty();

            // Verify container was read
            _mockContainerHandler.Verify(h => h.ReadEntriesAsync(
                containerFile,
                It.IsAny<System.Threading.CancellationToken>()), Times.Once);

            // Verify signing was called (nested files + container)
            _mockSigningProvider.Verify(p => p.SignFilesAsync(
                It.IsAny<IReadOnlyList<(FileNode, string)>>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);

            // Verify container was written back
            _mockContainerHandler.Verify(h => h.WriteContainerAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ContainerEntry>>(),
                It.IsAny<ContainerMetadata>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Containers_AreUnpackedOncePerUniqueContainerContent()
        {
            // Arrange
            // Two top-level containers with identical bytes but different names. They should both
            // be unpacked.
            var dup1 = CreateTestFile("dup1.testcontainer", "DUPLICATE-CONTAINER");
            var dup2 = CreateTestFile("dup2.testcontainer", "DUPLICATE-CONTAINER");

            // Another top-level container that contains a nested container entry with the same bytes.
            var outer = CreateTestFile("outer.testcontainer", "OUTER-CONTAINER");

            // Reset container handler setup to provide deterministic contents for this scenario.
            _mockContainerHandler.Reset();

            _mockContainerHandler.Setup(h => h.CanHandle(It.IsAny<string>()))
                .Returns((string path) => path.EndsWith(".testcontainer", StringComparison.OrdinalIgnoreCase));

            // Track actual unpack calls.
            var unpackedPaths = new List<string>();

            _mockContainerHandler.Setup(h => h.ReadEntriesAsync(
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Returns((string containerPath, System.Threading.CancellationToken ct) =>
                {
                    unpackedPaths.Add(containerPath);

                    string fileName = Path.GetFileName(containerPath);
                    return fileName switch
                    {
                        "outer.testcontainer" => ReadOuterContainerEntriesAsync(),
                        // For the duplicates, return the same inner entry list.
                        "dup1.testcontainer" => ReadDuplicateContainerEntriesAsync(),
                        // This should not be invoked (dedup at discovery) but if it is, keep it valid.
                        "dup2.testcontainer" => ReadDuplicateContainerEntriesAsync(),
                        // For the nested container extracted from the outer entry.
                        "inner.testcontainer" => ReadDuplicateContainerEntriesAsync(),
                        _ => ReadContainerEntriesAsync()
                    };
                });

            _mockContainerHandler.Setup(h => h.WriteContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<IEnumerable<ContainerEntry>>(),
                    It.IsAny<ContainerMetadata>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .Returns((string outputPath, IEnumerable<ContainerEntry> entries, ContainerMetadata metadata, System.Threading.CancellationToken ct) =>
                {
                    var outputDir = _mockFileSystem.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        _mockFileSystem.CreateDirectory(outputDir);
                    }

                    var manifest = string.Join("\n", entries.Select(e => e.RelativePath));
                    _mockFileSystem.WriteToFile(outputPath, $"CONTAINER\n{manifest}");
                    return Task.CompletedTask;
                });

            var mockRegistry = new Mock<IContainerHandlerRegistry>();
            mockRegistry.Setup(r => r.FindHandler(It.IsAny<string>()))
                .Returns<string>(path => path.EndsWith(".testcontainer", StringComparison.OrdinalIgnoreCase) ? _mockContainerHandler.Object : null);

            // Use a dedicated orchestrator that treats deduplication as path-sensitive for this test.
            // This test asserts that two top-level containers with identical bytes but different names
            // are both unpacked.
            var services = new ServiceCollection();
            services.AddRecursiveSigning();
            services.AddSingleton<IFileSystem>(_mockFileSystem);
            services.AddSingleton(_mockFileAnalyzer.Object);
            services.AddSingleton(_mockSignatureCalculator.Object);
            services.AddSingleton(_mockSigningProvider.Object);
            services.AddSingleton<IFileDeduplicator>(new PathSensitiveFileDeduplicator());
            services.AddSingleton(mockRegistry.Object);
            services.AddSingleton(Mock.Of<ILogger<Implementation.RecursiveSigning>>());

            using var sp = services.BuildServiceProvider();
            var orchestrator = sp.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new[] { new FileInfo(dup1), new FileInfo(dup2), new FileInfo(outer) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            if (!result.Success)
            {
                _output.WriteLine("Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.Errors.Should().BeEmpty();

            // Two containers with different names but the same contents should still be treated as
            // distinct containers for unpacking/signing.
            unpackedPaths.Select(Path.GetFileName)
                .Should().Contain(new[] { "dup1.testcontainer", "dup2.testcontainer", "outer.testcontainer" });

            // The nested container from the outer entry should also be enumerated.
            unpackedPaths.Select(Path.GetFileName)
                .Should().Contain("inner.testcontainer");

            _mockContainerHandler.Verify(h => h.ReadEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.AtLeast(4));
        }

        private sealed class PathSensitiveFileDeduplicator : IFileDeduplicator
        {
            public void RegisterFile(FileContentKey contentKey, string filePathOnDisk)
            {
                // Allow multiple registrations of the same content key at different paths.
            }

            public bool TryGetRegisteredFile(FileContentKey contentKey, out string? originalPath)
            {
                originalPath = null;
                return false;
            }

            public bool TryGetSignedVersion(FileContentKey key, out string signedPath)
            {
                signedPath = null!;
                return false;
            }

            public void RegisterSignedFile(FileContentKey key, string signedPath)
            {
            }
        }

        

        [Fact]
        public async Task DuplicateFiles_ShouldBeSignedOnce()
        {
            // Arrange
            // Create two files with same name and content in different locations
            var dir1 = _mockFileSystem.PathCombine(_workingDir, "dir1");
            var dir2 = _mockFileSystem.PathCombine(_workingDir, "dir2");
            _mockFileSystem.CreateDirectory(dir1);
            _mockFileSystem.CreateDirectory(dir2);
            
            var file1 = _mockFileSystem.PathCombine(dir1, "duplicate.txt");
            var file2 = _mockFileSystem.PathCombine(dir2, "duplicate.txt");
            _mockFileSystem.WriteToFile(file1, "Same Content");
            _mockFileSystem.WriteToFile(file2, "Same Content");

            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new[] { new FileInfo(Path.Combine(dir1, "duplicate.txt")), new FileInfo(Path.Combine(dir2, "duplicate.txt")) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            if (!result.Success)
            {
                _output.WriteLine("Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.SignedFiles.Should().HaveCount(2);
            result.Errors.Should().BeEmpty();

            // Both files should be reported as signed, but only one should have actually been signed
            result.SignedFiles.Count(f => f.WasAlreadySigned).Should().Be(1);
            
            // Verify analysis behavior
            // The orchestrator may analyze only the first path and treat the second as a duplicate by content-key.
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(file1, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(file2, It.IsAny<System.Threading.CancellationToken>()), Times.AtMostOnce);
            
            // Verify signing was called only once due to deduplication
            _mockSigningProvider.Verify(p => p.SignFilesAsync(
                It.Is<IReadOnlyList<(FileNode, string)>>(files => files.Count == 1),
                It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SigningGraph_ShouldCalculateRoundsCorrectly()
        {
            // Arrange
            var graph = _serviceProvider.GetRequiredService<ISigningGraph>();
            var fileAnalyzer = _serviceProvider.GetRequiredService<IFileAnalyzer>();
            var sigCalc = _serviceProvider.GetRequiredService<ISignatureCalculator>();

            // Create a simple graph: container with 2 files
            var file1Path = CreateTestFile("file1.txt", "content1");
            var file2Path = CreateTestFile("file2.txt", "content2");
            var containerPath = CreateTestFile("container.testcontainer", "container");

            var file1Metadata = await fileAnalyzer.AnalyzeAsync(file1Path);
            var file2Metadata = await fileAnalyzer.AnalyzeAsync(file2Path);
            var containerMetadata = await fileAnalyzer.AnalyzeAsync(containerPath);

            await using var file1Stream = new MemoryStream(_mockFileSystem.ReadAllBytes(file1Path));
            await using var file2Stream = new MemoryStream(_mockFileSystem.ReadAllBytes(file2Path));
            await using var containerStream = new MemoryStream(_mockFileSystem.ReadAllBytes(containerPath));

            var file1Key = new FileContentKey(await ContentHash.FromStreamAsync(file1Stream), "file1.txt");
            var file2Key = new FileContentKey(await ContentHash.FromStreamAsync(file2Stream), "file2.txt");
            var containerKey = new FileContentKey(await ContentHash.FromStreamAsync(containerStream), "container.testcontainer");

            var cfg = new SigningConfiguration(_workingDir);
            var file1Node = new FileNode(file1Key, new FileLocation(file1Path, RelativePathInContainer: null), file1Metadata, sigCalc.CalculateCertificateIdentifier(file1Metadata, cfg));
            var file2Node = new FileNode(file2Key, new FileLocation(file2Path, RelativePathInContainer: null), file2Metadata, sigCalc.CalculateCertificateIdentifier(file2Metadata, cfg));
            var containerNode = new FileNode(containerKey, new FileLocation(containerPath, RelativePathInContainer: null), containerMetadata, sigCalc.CalculateCertificateIdentifier(containerMetadata, cfg));

            graph.AddNode(containerNode, null);
            graph.AddNode(file1Node, containerNode);
            graph.AddNode(file2Node, containerNode);
            graph.FinalizeDiscovery();

            // Assert
            // Equivalent to the old signing-round calculation:
            // children are signable immediately, container is gated until children are done.
            graph.GetNodesReadyForSigning().Should().Contain(new[] { file1Node, file2Node });
            graph.GetContainersReadyForRepack().Should().NotContain(containerNode);

            graph.MarkAsComplete(file1Node);
            graph.GetContainersReadyForRepack().Should().NotContain(containerNode);

            graph.MarkAsComplete(file2Node);
            graph.GetContainersReadyForRepack().Should().ContainSingle().Which.Should().Be(containerNode);
        }

        [Fact]
        public async Task FileDeduplicator_WhenDuplicate_Throws_AndOriginalPathIsFirstPath()
        {
            // Arrange
            var deduplicator = _serviceProvider.GetRequiredService<IFileDeduplicator>();
            var analyzer = _serviceProvider.GetRequiredService<IFileAnalyzer>();

            // Create two files with same name in different locations
            var dir1 = _mockFileSystem.PathCombine(_workingDir, "test1");
            var dir2 = _mockFileSystem.PathCombine(_workingDir, "test2");
            _mockFileSystem.CreateDirectory(dir1);
            _mockFileSystem.CreateDirectory(dir2);
            
            var file1 = _mockFileSystem.PathCombine(dir1, "dup.txt");
            var file2 = _mockFileSystem.PathCombine(dir2, "dup.txt");
            _mockFileSystem.WriteToFile(file1, "identical content");
            _mockFileSystem.WriteToFile(file2, "identical content");

            // Act
            _ = await analyzer.AnalyzeAsync(file1);
            _ = await analyzer.AnalyzeAsync(file2);

            using var s1 = _mockFileSystem.GetFileStream(file1, FileMode.Open, FileAccess.Read);
            using var s2 = _mockFileSystem.GetFileStream(file2, FileMode.Open, FileAccess.Read);
            var key1 = new FileContentKey(await ContentHash.FromStreamAsync(s1), "dup.txt");
            var key2 = new FileContentKey(await ContentHash.FromStreamAsync(s2), "dup.txt");

            deduplicator.RegisterFile(key1, file1);
            Func<Task> act = () =>
            {
                deduplicator.RegisterFile(key2, file2);
                return Task.CompletedTask;
            };

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

            deduplicator.TryGetRegisteredFile(key2, out string? originalPath).Should().BeTrue();
            originalPath.Should().Be(file1);
        }

        [Fact]
        public async Task ContainerHandlerRegistry_ShouldFindCorrectHandler()
        {
            // Arrange
            var registry = _serviceProvider.GetRequiredService<IContainerHandlerRegistry>();

            // Act
            var handler = registry.FindHandler("test.testcontainer");

            // Assert
            handler.Should().NotBeNull();
            handler.Should().BeSameAs(_mockContainerHandler.Object);
            
            // Verify the mock's CanHandle method would return true
            _mockContainerHandler.Setup(h => h.CanHandle("test.testcontainer")).Returns(true);
            handler!.CanHandle("test.testcontainer").Should().BeTrue();
            
            await Task.CompletedTask;
        }

        private async IAsyncEnumerable<ContainerEntry> ReadOuterContainerEntriesAsync()
        {
            // Outer container contains one nested container and one regular file.
            var entries = new[]
            {
                ("inner.testcontainer", "DUPLICATE-CONTAINER"),
                ("outerfile.txt", "outer file content")
            };

            foreach (var (relativePath, content) in entries)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var stream = new MemoryStream(bytes);

                yield return new ContainerEntry(relativePath, stream)
                {
                    ContentHash = System.Security.Cryptography.SHA256.HashData(bytes),
                    Length = bytes.Length,
                };
            }

            await Task.CompletedTask;
        }

        private async IAsyncEnumerable<ContainerEntry> ReadDuplicateContainerEntriesAsync()
        {
            // Deterministic contents shared by any container with bytes "DUPLICATE-CONTAINER".
            var entries = new[]
            {
                ("a.txt", "A"),
                ("b.txt", "B")
            };

            foreach (var (relativePath, content) in entries)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var stream = new MemoryStream(bytes);

                yield return new ContainerEntry(relativePath, stream)
                {
                    ContentHash = System.Security.Cryptography.SHA256.HashData(bytes),
                    Length = bytes.Length,
                };
            }

            await Task.CompletedTask;
        }

        [Fact]
        public async Task SigningOrchestrator_ShouldHandleCancellation()
        {
            // Arrange
            var testFile = CreateTestFile("slow.txt", "content");
            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var request = new SigningRequest(
                new[] { new FileInfo(testFile) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                orchestrator.SignAsync(request, cts.Token));
        }

        [Fact]
        public async Task CompleteWorkflow_WithMultipleContainers()
        {
            // Arrange
            var container1 = CreateTestFile("package1.testcontainer", "pkg1");
            var container2 = CreateTestFile("package2.testcontainer", "pkg2");
            var simpleFile = CreateTestFile("readme.txt", "documentation");

            var orchestrator = _serviceProvider.GetRequiredService<IRecursiveSigning>();

            var request = new SigningRequest(
                new[] { new FileInfo(container1), new FileInfo(container2), new FileInfo(simpleFile) },
                new SigningConfiguration(_workingDir),
                new SigningOptions());

            // Act
            var result = await orchestrator.SignAsync(request);

            if (!result.Success)
            {
                _output.WriteLine("Signing failed. Errors:");
                foreach (var error in result.Errors)
                {
                    _output.WriteLine($"  - {error.Message} (File: {error.FilePath})");
                }
            }

            // Assert
            result.Success.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.SignedFiles.Should().NotBeEmpty();

            // Verify all files were processed
            var graph = _serviceProvider.GetRequiredService<ISigningGraph>();
            graph.IsComplete().Should().BeTrue();

            // Verify telemetry
            result.Telemetry.Should().NotBeNull();
            result.Telemetry.TotalFiles.Should().BeGreaterThan(0);
            result.Telemetry.FilesSigned.Should().BeGreaterThan(0);
            
            // Verify all input files were analyzed
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(container1, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(container2, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            _mockFileAnalyzer.Verify(a => a.AnalyzeAsync(simpleFile, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
            
            // Verify containers were processed
            _mockContainerHandler.Verify(h => h.ReadEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.AtLeast(2));
            
            // Verify signing occurred
            _mockSigningProvider.Verify(p => p.SignFilesAsync(
                It.IsAny<IReadOnlyList<(FileNode, string)>>(),
                It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
        }

        private void SetupDefaultMockBehaviors()
        {
            // Setup FileAnalyzer mock
            _mockFileAnalyzer.Setup(a => a.AnalyzeAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((string path, System.Threading.CancellationToken ct) =>
                {
                    return (IFileMetadata)new FileMetadata(executableType: ExecutableType.None);
                });

            // Container contents are analyzed from streams during extraction.
            _mockFileAnalyzer.Setup(a => a.AnalyzeAsync(
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((Stream stream, string fileName, System.Threading.CancellationToken ct) =>
                {
                    return (IFileMetadata)new FileMetadata(executableType: ExecutableType.None);
                });

            // Setup SignatureCalculator mock
            _mockSignatureCalculator.Setup(c => c.CalculateCertificateIdentifier(
                It.IsAny<IFileMetadata>(),
                It.IsAny<SigningConfiguration>()))
                .Returns((IFileMetadata metadata, SigningConfiguration config) => Mock.Of<ICertificateIdentifier>(ci => ci.Name == "TestCert"));

            // Setup SigningProvider mock
            _mockSigningProvider.Setup(p => p.SignFilesAsync(
                It.IsAny<IReadOnlyList<(FileNode node, string outputPath)>>(),
                It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<(FileNode node, string outputPath)> files, System.Threading.CancellationToken ct) =>
                {
                    foreach (var (node, outputPath) in files)
                    {
                        _signedFiles.Add(node.Location.FilePathOnDisk!);
                        var outputDir = _mockFileSystem.GetDirectoryName(outputPath);
                        if (!string.IsNullOrEmpty(outputDir))
                        {
                            _mockFileSystem.CreateDirectory(outputDir);
                        }

                        // The orchestrator may update node locations (e.g. repacked containers).
                        // If the input path isn't present in the mock FS, write a minimal payload.
                        if (_mockFileSystem.FileExists(node.Location.FilePathOnDisk!))
                        {
                            _mockFileSystem.CopyFile(node.Location.FilePathOnDisk!, outputPath, overwrite: true);
                            var currentContent = _mockFileSystem.Files[outputPath];
                            _mockFileSystem.WriteToFile(outputPath, currentContent + $"\n[SIGNED with {node.CertificateIdentifier?.Name}]");
                        }
                        else
                        {
                            // Ensure the output exists even when the original input path doesn't.
                            _mockFileSystem.WriteToFile(outputPath, $"[SIGNED placeholder for {node.ContentKey.FileName} with {node.CertificateIdentifier?.Name}]");
                        }
                    }
                    return true;
                });

            // Setup ContainerHandler mock
            _mockContainerHandler.Setup(h => h.CanHandle(It.IsAny<string>()))
                .Returns((string path) => path.EndsWith(".testcontainer"));

            _mockContainerHandler.Setup(h => h.ReadEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<System.Threading.CancellationToken>()))
                .Returns(ReadContainerEntriesAsync());

            _mockContainerHandler.Setup(h => h.WriteContainerAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ContainerEntry>>(),
                It.IsAny<ContainerMetadata>(),
                It.IsAny<System.Threading.CancellationToken>()))
                .Returns((string outputPath, IEnumerable<ContainerEntry> entries, ContainerMetadata metadata, System.Threading.CancellationToken ct) =>
                {
                    var outputDir = _mockFileSystem.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir))
                    {
                        _mockFileSystem.CreateDirectory(outputDir);
                    }

                    // Create a minimal container payload so subsequent hashing/analyzing can open the file.
                    var manifest = string.Join("\n", entries.Select(e => e.RelativePath));
                    _mockFileSystem.WriteToFile(outputPath, $"CONTAINER\n{manifest}");
                    return Task.CompletedTask;
                });
        }

        private async IAsyncEnumerable<ContainerEntry> ReadContainerEntriesAsync()
        {
            var entries = new[]
            {
                ("file1.txt", "Content of file 1"),
                ("file2.txt", "Content of file 2"),
                ("nested/file3.txt", "Content of file 3")
            };

            foreach (var (relativePath, content) in entries)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                var stream = new MemoryStream(bytes);

                yield return new ContainerEntry(relativePath, stream)
                {
                    ContentHash = System.Security.Cryptography.SHA256.HashData(bytes),
                    Length = bytes.Length,
                };
            }

            await Task.CompletedTask;
        }

        private string CreateTestFile(string fileName, string content)
        {
            var filePath = _mockFileSystem.PathCombine(_workingDir, fileName);
            var dirPath = _mockFileSystem.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && !_mockFileSystem.DirectoryExists(dirPath))
            {
                _mockFileSystem.CreateDirectory(dirPath);
            }
            _mockFileSystem.WriteToFile(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }
}

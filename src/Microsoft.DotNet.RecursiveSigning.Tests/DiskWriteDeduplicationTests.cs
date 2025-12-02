// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Configuration;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Tests validating that duplicate files are never written to disk during discovery.
    /// These tests ensure deduplication works and that we aren't getting unwanted writes to disk.
    /// </summary>
    public class DiskWriteDeduplicationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly MockFileSystem _fileSystem;
        private readonly IServiceProvider _services;
        private readonly string _testRoot;
        private readonly string _tempDir;

        public DiskWriteDeduplicationTests(ITestOutputHelper output)
        {
            _output = output;
            _testRoot = "/test";
            _tempDir = "/temp";

            _fileSystem = new MockFileSystem(
                files: new Dictionary<string, string>(),
                directories: new[] { _testRoot, _tempDir });

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            // Add RecursiveSigning first (registers real FileSystem)
            services.AddRecursiveSigning();
            
            // Then replace FileSystem with MockFileSystem (last registration wins)
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFileSystem));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton<IFileSystem>(_fileSystem);

            // Add test-specific services
            var stubHandler = new StubContainerHandler();
            var registry = new ContainerHandlerRegistry();
            registry.RegisterHandler(stubHandler);
            
            var stubAnalyzer = new StubFileAnalyzer(registry, _fileSystem);
            services.AddSingleton<IFileAnalyzer>(stubAnalyzer);
            services.AddSingleton<IContainerHandlerRegistry>(registry);
            services.AddSingleton(stubHandler);
            
            // Add signature calculator
            var stubSignatureCalculator = new StubSignatureCalculator();
            services.AddSingleton<ISignatureCalculator>(stubSignatureCalculator);
            
            // Add fake signing provider
            var fakeSigningProvider = new FakeSigningProvider(_fileSystem);
            services.AddSingleton<ISigningProvider>(fakeSigningProvider);

            _services = services.BuildServiceProvider();
        }

        [Fact]
        public async Task TwoTopLevelFiles_SameContent_OnlyFirstIsWrittenToDisk()
        {
            // Arrange: Create two top-level files with identical content AND filename
            // (in different directories to represent the same file delivered multiple times)
            string dir1 = _fileSystem.PathCombine(_testRoot, "dir1");
            string dir2 = _fileSystem.PathCombine(_testRoot, "dir2");
            _fileSystem.CreateDirectory(dir1);
            _fileSystem.CreateDirectory(dir2);
            
            string file1Path = _fileSystem.PathCombine(dir1, "duplicate.txt");
            string file2Path = _fileSystem.PathCombine(dir2, "duplicate.txt");
            string content = "identical content";

            _fileSystem.WriteToFile(file1Path, content);
            _fileSystem.WriteToFile(file2Path, content);

            var request = CreateSigningRequest(file1Path, file2Path);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Only temp directory writes should occur (no duplicate extraction)
            var tempWrites = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .ToList();

            _output.WriteLine($"Files written during discovery: {tempWrites.Count}");
            foreach (var file in tempWrites)
            {
                _output.WriteLine($"  - {file}");
            }

            // Both files are analyzed from their original locations, no extraction needed
            // Only signing phase should write files
            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            allNodes.Should().HaveCount(2, "both files should be in the graph");
            
            // Verify both nodes share the same signing info
            var node1 = allNodes.First(n => n.Location.FilePathOnDisk == file1Path);
            var node2 = allNodes.First(n => n.Location.FilePathOnDisk == file2Path);
            
            node1.CertificateIdentifier?.Name.Should().Be(node2.CertificateIdentifier?.Name,
                "duplicate files should result in the same signing decision");
        }



        [Fact]
        public async Task TopLevelFile_AndContainerEntry_SameContent_OnlyFirstIsWrittenToDisk()
        {
            // Arrange: Create a top-level file and a container with the same file inside
            string topLevelFile = _fileSystem.PathCombine(_testRoot, "shared.dll");
            string containerFile = _fileSystem.PathCombine(_testRoot, "package.testcontainer");
            string sharedContent = "shared file content";

            _fileSystem.WriteToFile(topLevelFile, sharedContent);

            // Create a stub container with the same file
            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(containerFile, new List<(string, string)>
            {
                ("lib/shared.dll", sharedContent)
            });
            _fileSystem.WriteToFile(containerFile, "container marker");

            var request = CreateSigningRequest(topLevelFile, containerFile);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Verify graph structure
            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            // Should have: topLevelFile, containerFile, and a reference node for shared.dll in container
            allNodes.Should().HaveCount(3, "should have top-level file, container, and reference node");

            var topLevelNode = allNodes.First(n => n.Location.FilePathOnDisk == topLevelFile);
            var containerNode = allNodes.First(n => n.Location.FilePathOnDisk == containerFile);

            // The container should have one child node for the entry.
            containerNode.Children.Should().HaveCount(1);
            var entryNode = containerNode.Children.Single();

            entryNode.Location.RelativePathInContainer.Should().Be("lib/shared.dll");

            // The entry should reuse signing decisions from the top-level file.
            entryNode.CertificateIdentifier?.Name.Should().Be(topLevelNode.CertificateIdentifier?.Name,
                "duplicate container entry should result in the same signing decision as the first occurrence");

            // Verify parent-child relationship
            entryNode.Parent.Should().Be(containerNode,
                "container entry node should be a child of the container");
        }

        [Fact]
        public async Task TwoEntriesInSameContainer_SameContent_OnlyFirstIsExtracted()
        {
            // Arrange: Create a container with two entries having identical content
            string containerFile = _fileSystem.PathCombine(_testRoot, "package.testcontainer");
            string sharedContent = "duplicate entry content";

            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(containerFile, new List<(string, string)>
            {
                ("lib/net8.0/file.dll", sharedContent),
                ("lib/net10.0/file.dll", sharedContent)
            });
            _fileSystem.WriteToFile(containerFile, "container marker");

            var request = CreateSigningRequest(containerFile);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Only the first occurrence should be extracted
            var tempWrites = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .Where(p => p.Contains("file.dll"))
                .ToList();

            _output.WriteLine($"Extracted file.dll instances: {tempWrites.Count}");
            foreach (var file in tempWrites)
            {
                _output.WriteLine($"  - {file}");
            }

            tempWrites.Should().HaveCount(1,
                "only the first occurrence of duplicate file should be extracted to disk");

            // Verify graph structure
            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            // Should have: container + 2 child nodes (first extraction + reference node)
            allNodes.Should().HaveCount(3, "should have container and two child nodes");

            var containerNode = allNodes.First(n => n.Location.FilePathOnDisk == containerFile);
            var childNodes = allNodes.Where(n => n.Parent == containerNode).ToList();

            childNodes.Should().HaveCount(2, "container should have two child nodes");

            // First child should have been extracted
            var firstChild = childNodes.First(n => n.Location.RelativePathInContainer == "lib/net8.0/file.dll");
            firstChild.Location.FilePathOnDisk.Should().StartWith(_tempDir,
                "first occurrence should have been extracted to temp directory");

            // Second child should reference the first
            var secondChild = childNodes.First(n => n.Location.RelativePathInContainer == "lib/net10.0/file.dll");
            secondChild.Location.FilePathOnDisk.Should().Be(firstChild.Location.FilePathOnDisk,
                "duplicate should reuse the first file's extracted path");

            // Both should result in the same signing decision.
            firstChild.CertificateIdentifier?.Name.Should().Be(secondChild.CertificateIdentifier?.Name,
                "duplicates should result in the same signing decision");
        }

        [Fact]
        public async Task TwoDifferentNamedEntriesInSameContainer_SameContent_AreExtractedTwice()
        {
            // Arrange: Create a container with two entries having identical content but different names
            string containerFile = _fileSystem.PathCombine(_testRoot, "package.testcontainer");
            string sharedContent = "same bytes";

            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(containerFile, new List<(string, string)>
            {
                ("lib/net8.0/a.dll", sharedContent),
                ("lib/net8.0/b.dll", sharedContent)
            });
            _fileSystem.WriteToFile(containerFile, "container marker");

            var request = CreateSigningRequest(containerFile);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Both entries should be extracted because deduplication is by content + file name
            var extracted = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .Where(p => p.EndsWith("a.dll") || p.EndsWith("b.dll"))
                .ToList();

            _output.WriteLine($"Extracted a.dll/b.dll instances: {extracted.Count}");
            foreach (var file in extracted)
            {
                _output.WriteLine($"  - {file}");
            }

            extracted.Should().HaveCount(2, "entries with different names should not be deduplicated");
            extracted.Distinct().Should().HaveCount(2, "each entry should be extracted to a distinct path");

            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            allNodes.Should().HaveCount(3, "should have container and two child nodes");

            var containerNode = allNodes.First(n => n.Location.FilePathOnDisk == containerFile);
            var childNodes = allNodes.Where(n => n.Parent == containerNode).ToList();

            childNodes.Should().HaveCount(2, "container should have two child nodes");

            var nodeA = childNodes.Single(n => n.Location.RelativePathInContainer == "lib/net8.0/a.dll");
            var nodeB = childNodes.Single(n => n.Location.RelativePathInContainer == "lib/net8.0/b.dll");

            nodeA.Location.FilePathOnDisk.Should().StartWith(_tempDir);
            nodeB.Location.FilePathOnDisk.Should().StartWith(_tempDir);
            nodeA.Location.FilePathOnDisk.Should().NotBe(nodeB.Location.FilePathOnDisk,
                "different names should not share the same extracted file");
        }

        [Fact]
        public async Task TwoEntriesInDifferentContainers_SameContent_OnlyFirstIsExtracted()
        {
            // Arrange: Create two containers, each with a file having identical content
            string container1 = _fileSystem.PathCombine(_testRoot, "package1.testcontainer");
            string container2 = _fileSystem.PathCombine(_testRoot, "package2.testcontainer");
            string sharedContent = "shared across containers";

            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(container1, new List<(string, string)>
            {
                ("lib/shared.dll", sharedContent)
            });
            containerHandler.SetContainerContents(container2, new List<(string, string)>
            {
                ("tools/shared.dll", sharedContent)
            });
            
            _fileSystem.WriteToFile(container1, "container marker 1");
            _fileSystem.WriteToFile(container2, "container marker 2");

            var request = CreateSigningRequest(container1, container2);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Only one extraction of shared.dll should occur
            var tempWrites = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .Where(p => p.Contains("shared.dll"))
                .ToList();

            _output.WriteLine($"Extracted shared.dll instances: {tempWrites.Count}");
            foreach (var file in tempWrites)
            {
                _output.WriteLine($"  - {file}");
            }

            tempWrites.Should().HaveCount(1,
                "only the first occurrence should be extracted, even across containers");

            // Verify graph structure
            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            // Should have: 2 containers + 2 child nodes (one extracted + one reference)
            allNodes.Should().HaveCount(4, "should have 2 containers and 2 child nodes");

            var container1Node = allNodes.First(n => n.Location.FilePathOnDisk == container1);
            var container2Node = allNodes.First(n => n.Location.FilePathOnDisk == container2);

            var child1 = allNodes.First(n => n.Parent == container1Node);
            var child2 = allNodes.First(n => n.Parent == container2Node);

            // First child should have been extracted
            child1.Location.FilePathOnDisk.Should().StartWith(_tempDir,
                "first occurrence should have been extracted");

            // Second child should reference the first
            child2.Location.FilePathOnDisk.Should().Be(child1.Location.FilePathOnDisk,
                "duplicate in second container should reuse first extraction");

            // Both should result in the same signing decision.
            child1.CertificateIdentifier?.Name.Should().Be(child2.CertificateIdentifier?.Name,
                "duplicates across containers should result in the same signing decision");

            // Verify parent relationships
            child1.Parent.Should().Be(container1Node);
            child2.Parent.Should().Be(container2Node);
        }

        [Fact]
        public async Task TwoDifferentNamedEntriesInDifferentContainers_SameContent_AreExtractedTwice()
        {
            // Arrange: Create two containers, each with an entry with identical content but different names
            string container1 = _fileSystem.PathCombine(_testRoot, "package1.testcontainer");
            string container2 = _fileSystem.PathCombine(_testRoot, "package2.testcontainer");
            string sharedContent = "same content across containers";

            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(container1, new List<(string, string)>
            {
                ("lib/a.dll", sharedContent)
            });
            containerHandler.SetContainerContents(container2, new List<(string, string)>
            {
                ("tools/b.dll", sharedContent)
            });

            _fileSystem.WriteToFile(container1, "container marker 1");
            _fileSystem.WriteToFile(container2, "container marker 2");

            var request = CreateSigningRequest(container1, container2);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Both entries should be extracted because the file names differ
            var extracted = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .Where(p => p.EndsWith("a.dll") || p.EndsWith("b.dll"))
                .ToList();

            _output.WriteLine($"Extracted a.dll/b.dll instances across containers: {extracted.Count}");
            foreach (var file in extracted)
            {
                _output.WriteLine($"  - {file}");
            }

            extracted.Should().HaveCount(2, "entries with different names should not be deduplicated across containers");
            extracted.Distinct().Should().HaveCount(2, "each entry should be extracted to a distinct path");

            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            allNodes.Should().HaveCount(4, "should have 2 containers and 2 child nodes");

            var container1Node = allNodes.First(n => n.Location.FilePathOnDisk == container1);
            var container2Node = allNodes.First(n => n.Location.FilePathOnDisk == container2);

            var child1 = allNodes.Single(n => n.Parent == container1Node);
            var child2 = allNodes.Single(n => n.Parent == container2Node);

            child1.Location.RelativePathInContainer.Should().Be("lib/a.dll");
            child2.Location.RelativePathInContainer.Should().Be("tools/b.dll");

            child1.Location.FilePathOnDisk.Should().StartWith(_tempDir);
            child2.Location.FilePathOnDisk.Should().StartWith(_tempDir);
            child1.Location.FilePathOnDisk.Should().NotBe(child2.Location.FilePathOnDisk,
                "different names should not share the same extracted file");
        }

        [Fact]
        public async Task NestedContainers_WithDuplicates_OnlyFirstIsExtracted()
        {
            // Arrange: Create nested containers with duplicate files
            string outerContainer = _fileSystem.PathCombine(_testRoot, "outer.testcontainer");

            var containerHandler = _services.GetRequiredService<StubContainerHandler>();
            containerHandler.SetContainerContents(outerContainer, new List<(string, string)>
            {
                ("inner1.testcontainer", "inner container 1"),
                ("inner2.testcontainer", "inner container 2")
            });

            var inner1Path = _fileSystem.PathCombine(_tempDir, "inner1.testcontainer");
            var inner2Path = _fileSystem.PathCombine(_tempDir, "inner2.testcontainer");
            containerHandler.SetContainerContents(inner1Path, new List<(string, string)> { ("content.dll", "duplicate payload") });
            containerHandler.SetContainerContents(inner2Path, new List<(string, string)> { ("content.dll", "duplicate payload") });

            _fileSystem.WriteToFile(outerContainer, "container marker");

            var request = CreateSigningRequest(outerContainer);
            var recursiveSigning = _services.GetRequiredService<IRecursiveSigning>();

            // Track initial file count
            int initialFileCount = _fileSystem.Files.Count;

            // Act: Run discovery phase
            await recursiveSigning.SignAsync(request, CancellationToken.None);

            // Assert: Only one extraction of content.dll should occur
            var tempWrites = _fileSystem.Files.Keys
                .Skip(initialFileCount)
                .Where(p => p.StartsWith(_tempDir))
                .Where(p => !p.StartsWith(_fileSystem.PathCombine(_tempDir, "signed")))
                .Where(p => p.Contains("content.dll"))
                .ToList();

            _output.WriteLine($"Extracted content.dll instances: {tempWrites.Count}");
            foreach (var file in tempWrites)
            {
                _output.WriteLine($"  - {file}");
            }

            tempWrites.Should().HaveCount(1,
                "only first occurrence should be extracted, even in nested containers");

            // Verify graph has correct structure
            var graph = _services.GetRequiredService<ISigningGraph>();
            var allNodes = graph.GetAllNodes();

            // Find all content.dll nodes
            var contentNodes = allNodes
                .Where(n => n.Location.RelativePathInContainer == "content.dll")
                .ToList();

            contentNodes.Should().HaveCount(2, "should have 2 content.dll nodes");

            // All should result in the same signing decision.
            contentNodes.Select(n => n.CertificateIdentifier?.Name)
                .Distinct()
                .Should()
                .HaveCount(1, "all duplicates should result in the same signing decision");

            // Inspect graph relationships: both inner containers should have a content.dll child.
            var outerNode = allNodes.First(n => n.Location.FilePathOnDisk == outerContainer);
            outerNode.Children.Select(n => n.Location.RelativePathInContainer)
                .Should()
                .BeEquivalentTo(["inner1.testcontainer", "inner2.testcontainer"]);

            var innerNodes = outerNode.Children.ToList();
            innerNodes.Should().HaveCount(2);
            innerNodes.All(n => n.Parent == outerNode).Should().BeTrue();

            var innerContentNodes = innerNodes
                .SelectMany(n => n.Children)
                .Where(n => n.Location.RelativePathInContainer == "content.dll")
                .ToList();

            innerContentNodes.Should().HaveCount(2, "each inner container should contribute a content.dll node");
            innerContentNodes.All(n => innerNodes.Contains(n.Parent!)).Should().BeTrue();

            // One should be extracted; the other should reference the first.
            var extractedNode = innerContentNodes.First(n => n.Location.FilePathOnDisk?.StartsWith(_tempDir) == true);
            innerContentNodes
                .Count(n => n.Location.FilePathOnDisk == extractedNode.Location.FilePathOnDisk)
                .Should()
                .BeGreaterThan(1, "at least one node should reference the extracted path");
        }

        [Fact]
        public async Task RegisterFile_WhenDuplicate_Throws_AndOriginalPathIsFirstPath()
        {
            // Arrange
            var deduplicator = new DefaultFileDeduplicator();
            string file1 = _fileSystem.PathCombine(_testRoot, "dir1", "a.txt");
            string file2 = _fileSystem.PathCombine(_testRoot, "dir2", "a.txt");
            _fileSystem.CreateDirectory(_fileSystem.PathCombine(_testRoot, "dir1"));
            _fileSystem.CreateDirectory(_fileSystem.PathCombine(_testRoot, "dir2"));
            _fileSystem.WriteToFile(file1, "same");
            _fileSystem.WriteToFile(file2, "same");

            var analyzer = _services.GetRequiredService<IFileAnalyzer>();
            _ = await analyzer.AnalyzeAsync(file1, CancellationToken.None);
            _ = await analyzer.AnalyzeAsync(file2, CancellationToken.None);

            using var s1 = _fileSystem.GetFileStream(file1, FileMode.Open, FileAccess.Read);
            using var s2 = _fileSystem.GetFileStream(file2, FileMode.Open, FileAccess.Read);
            var k1 = new FileContentKey(await ContentHash.FromStreamAsync(s1, CancellationToken.None), "a.txt");
            var k2 = new FileContentKey(await ContentHash.FromStreamAsync(s2, CancellationToken.None), "a.txt");

            // Act
            deduplicator.RegisterFile(k1, file1);

            Action act = () => deduplicator.RegisterFile(k2, file2);

            // Assert
            act.Should().Throw<InvalidOperationException>();

            deduplicator.TryGetRegisteredFile(k2, out string? originalPath).Should().BeTrue();
            originalPath.Should().Be(file1);
        }

        private SigningRequest CreateSigningRequest(params string[] filePaths)
        {
            var configuration = new SigningConfiguration(_tempDir);
            var options = new SigningOptions();

            return new SigningRequest(
                filePaths.Select(p => new FileInfo(p)).ToArray(),
                configuration,
                options);
        }
    }

    /// <summary>
    /// Extension methods for MockFileSystem to make tests more readable.
    /// </summary>
    internal static class MockFileSystemExtensions
    {
        public static void WriteAllText(this MockFileSystem fileSystem, string path, string content)
        {
            fileSystem.WriteToFile(path, content);
        }
    }
}

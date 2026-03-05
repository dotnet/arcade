// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Configuration;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class RecursiveSigningZipRepackRegressionTests
    {
        [Fact]
        public async Task SignAsync_ZipContainer_RepackUpdatesRootInPlace()
        {
            string root = Path.Combine(Path.GetTempPath(), "arcade-rs-regression", Guid.NewGuid().ToString("N"));
            string tempDirectory = Path.Combine(root, "tmp");
            string inputDirectory = Path.Combine(root, "input");
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(inputDirectory);

            try
            {
                string containerPath = Path.Combine(inputDirectory, "sample.nupkg");
                CreateZip(containerPath, "content\\file.txt", "payload");

                var recursiveSigning = BuildRecursiveSigning();
                var request = new SigningRequest(
                    new[] { new FileInfo(containerPath) },
                    new SigningConfiguration(tempDirectory),
                    new SigningOptions());

                var result = await recursiveSigning.SignAsync(request);

                result.Success.Should().BeTrue();
                result.Errors.Should().BeEmpty();
                result.SignedFiles.Should().Contain(f => string.Equals(Path.GetFileName(f.FilePath), "sample.nupkg", StringComparison.OrdinalIgnoreCase));
                result.SignedFiles.Should().NotContain(f => f.FilePath.Contains($"{Path.DirectorySeparatorChar}repacked{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

                ReadZipEntry(containerPath, "content/file.txt").Should().Contain("[DRY-RUN SIGNED with TestCert]");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Fact]
        public async Task SignAsync_WithOutputDirectory_CopiesSignedRootFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "arcade-rs-regression", Guid.NewGuid().ToString("N"));
            string tempDirectory = Path.Combine(root, "tmp");
            string outputDirectory = Path.Combine(root, "output");
            string inputDirectory = Path.Combine(root, "input");
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(inputDirectory);

            try
            {
                string filePath = Path.Combine(inputDirectory, "payload.txt");
                await File.WriteAllTextAsync(filePath, "payload");

                var recursiveSigning = BuildRecursiveSigning();
                var request = new SigningRequest(
                    new[] { new FileInfo(filePath) },
                    new SigningConfiguration(tempDirectory, outputDirectory),
                    new SigningOptions());

                var result = await recursiveSigning.SignAsync(request);

                result.Success.Should().BeTrue();
                result.Errors.Should().BeEmpty();

                string outputPath = BuildExpectedOutputPath(filePath, outputDirectory, filePath);
                File.Exists(outputPath).Should().BeTrue();
                File.ReadAllText(filePath).Should().Be("payload");
                File.ReadAllText(outputPath).Should().Contain("[DRY-RUN SIGNED with TestCert]");
                result.SignedFiles.Should().Contain(f => string.Equals(f.FilePath, outputPath, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Fact]
        public async Task SignAsync_WithOutputDirectory_CopiesSignedRootContainer()
        {
            string root = Path.Combine(Path.GetTempPath(), "arcade-rs-regression", Guid.NewGuid().ToString("N"));
            string tempDirectory = Path.Combine(root, "tmp");
            string outputDirectory = Path.Combine(root, "output");
            string inputDirectory = Path.Combine(root, "input");
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(inputDirectory);

            try
            {
                string containerPath = Path.Combine(inputDirectory, "sample.nupkg");
                CreateZip(containerPath, "content\\file.txt", "payload");

                var recursiveSigning = BuildRecursiveSigning();
                var request = new SigningRequest(
                    new[] { new FileInfo(containerPath) },
                    new SigningConfiguration(tempDirectory, outputDirectory),
                    new SigningOptions());

                var result = await recursiveSigning.SignAsync(request);

                result.Success.Should().BeTrue();
                result.Errors.Should().BeEmpty();

                string outputPath = BuildExpectedOutputPath(containerPath, outputDirectory, containerPath);
                File.Exists(outputPath).Should().BeTrue();
                ReadZipEntry(containerPath, "content/file.txt").Should().Be("payload");
                ReadZipEntry(outputPath, "content/file.txt").Should().Contain("[DRY-RUN SIGNED with TestCert]");
                result.SignedFiles.Should().Contain(f => string.Equals(f.FilePath, outputPath, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Fact]
        public async Task SignAsync_WithOutputDirectory_StripsSharedInputRootForMultipleFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "arcade-rs-regression", Guid.NewGuid().ToString("N"));
            string tempDirectory = Path.Combine(root, "tmp");
            string outputDirectory = Path.Combine(root, "output");
            string inputRoot = Path.Combine(root, "input", "shared");
            string dirA = Path.Combine(inputRoot, "a");
            string dirB = Path.Combine(inputRoot, "b");
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(dirA);
            Directory.CreateDirectory(dirB);

            try
            {
                string fileA = Path.Combine(dirA, "one.txt");
                string fileB = Path.Combine(dirB, "two.txt");
                await File.WriteAllTextAsync(fileA, "A");
                await File.WriteAllTextAsync(fileB, "B");

                var recursiveSigning = BuildRecursiveSigning();
                var request = new SigningRequest(
                    new[] { new FileInfo(fileA), new FileInfo(fileB) },
                    new SigningConfiguration(tempDirectory, outputDirectory),
                    new SigningOptions());

                var result = await recursiveSigning.SignAsync(request);

                result.Success.Should().BeTrue();
                result.Errors.Should().BeEmpty();

                string outputA = Path.Combine(outputDirectory, "a", "one.txt");
                string outputB = Path.Combine(outputDirectory, "b", "two.txt");

                File.Exists(outputA).Should().BeTrue();
                File.Exists(outputB).Should().BeTrue();
                File.ReadAllText(fileA).Should().Be("A");
                File.ReadAllText(fileB).Should().Be("B");
                File.ReadAllText(outputA).Should().Contain("[DRY-RUN SIGNED with TestCert]");
                File.ReadAllText(outputB).Should().Contain("[DRY-RUN SIGNED with TestCert]");
                result.SignedFiles.Should().Contain(f => string.Equals(f.FilePath, outputA, StringComparison.OrdinalIgnoreCase));
                result.SignedFiles.Should().Contain(f => string.Equals(f.FilePath, outputB, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        private static IRecursiveSigning BuildRecursiveSigning()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddRecursiveSigning();
            services.AddContainerHandler<ZipContainerHandler>();
            services.AddSingleton<IFileAnalyzer, DefaultFileAnalyzer>();
            services.AddSingleton<ISigningProvider, DryRunSigningProvider>();

            var rules = new DefaultCertificateRules(
                certificatesByFriendlyName: new Dictionary<string, JsonElement>
                {
                    ["TestCert"] = JsonDocument.Parse("""{"friendlyName":"TestCert"}""").RootElement.Clone()
                },
                signRegardlessByFriendlyName: null,
                fileNameMappings: new Dictionary<string, string>(),
                fileExtensionMappings: new Dictionary<string, string>
                {
                    [".txt"] = "TestCert",
                    [".nupkg"] = "TestCert",
                });
            services.AddSingleton<ICertificateCalculator>(_ => new DefaultCertificateCalculator(rules));

            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IRecursiveSigning>();
        }

        private static string BuildExpectedOutputPath(string sourcePath, string outputDirectory, params string[] allInputs)
        {
            string commonRoot = GetCommonRootForFiles(allInputs);
            string relativePath = Path.GetRelativePath(commonRoot, Path.GetFullPath(sourcePath));
            return Path.Combine(outputDirectory, relativePath);
        }

        private static string GetCommonRootForFiles(params string[] inputPaths)
        {
            var directories = inputPaths
                .Select(path => Path.GetDirectoryName(Path.GetFullPath(path)) ?? Path.GetFullPath(path))
                .ToArray();

            string common = directories[0];
            foreach (string directory in directories.Skip(1))
            {
                common = GetCommonPrefix(common, directory);
            }

            return common;
        }

        private static string GetCommonPrefix(string first, string second)
        {
            string firstRoot = Path.GetPathRoot(first) ?? string.Empty;
            string secondRoot = Path.GetPathRoot(second) ?? string.Empty;
            if (!string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase))
            {
                return firstRoot;
            }

            string[] firstSegments = first.Substring(firstRoot.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            string[] secondSegments = second.Substring(secondRoot.Length).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            int segmentCount = Math.Min(firstSegments.Length, secondSegments.Length);
            int i = 0;
            while (i < segmentCount && string.Equals(firstSegments[i], secondSegments[i], StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }

            if (i == 0)
            {
                return firstRoot;
            }

            return Path.Combine(firstRoot, Path.Combine(firstSegments.Take(i).ToArray()));
        }

        private static string ReadZipEntry(string zipPath, string entryPath)
        {
            using var stream = File.Open(zipPath, FileMode.Open, FileAccess.Read);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = zip.GetEntry(entryPath)!;
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        private static void CreateZip(string path, string entryPath, string content)
        {
            using var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
            var entry = zip.CreateEntry(entryPath.Replace('\\', '/'));
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}

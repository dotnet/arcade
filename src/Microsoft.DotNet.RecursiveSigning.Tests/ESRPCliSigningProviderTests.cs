// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public class ESRPCliSigningProviderTests
    {
        private static readonly string RootDir = OperatingSystem.IsWindows() ? @"C:\build" : "/build";

        private static ESRPCliSigningConfiguration CreateConfig(bool dryRun = false) => new()
        {
            ESRPCliPath = "/tools/esrpcli.dll",
            GatewayUrl = "https://api.esrp.microsoft.com/api/v2",
            ClientId = "test-client-id",
            TenantId = "test-tenant-id",
            Organization = "Microsoft",
            OrganizationInfoUrl = "https://www.microsoft.com",
            KeyVaultName = "TestVault",
            CertificateName = "TestCert",
            AuthMode = ESRPAuthMode.Certificate,
            EncryptedAuthCertPath = "/certs/auth.enc",
            EncryptionKeyPath = "/certs/key.json",
            BatchSize = 400,
            TimeoutInMinutes = 30,
            TempDirectory = OperatingSystem.IsWindows() ? @"C:\temp" : "/tmp",
            RootDirectory = RootDir,
            DryRun = dryRun,
        };

        private static ESRPCertificateIdentifier CreateCert(string name, string keyCode = "CP-230012")
        {
            var opsJson = "[{\"KeyCode\":\"" + keyCode + "\",\"OperationCode\":\"SigntoolSign\",\"Parameters\":{\"OpusName\":\"Microsoft\"},\"ToolName\":\"sign\",\"ToolVersion\":\"1.0\"}]";
            var ops = JsonDocument.Parse(opsJson).RootElement;
            return new ESRPCertificateIdentifier(name, ops);
        }

        private static FileNode CreateFileNode(string relativePath, ESRPCertificateIdentifier cert)
        {
            var fullPath = (RootDir + "/" + relativePath).Replace('/', System.IO.Path.DirectorySeparatorChar);
            var contentKey = new FileContentKey(
                new ContentHash(ImmutableArray.Create<byte>(1, 2, 3, 4)),
                System.IO.Path.GetFileName(relativePath));
            var location = new FileLocation(fullPath, null);
            var metadata = new FileMetadata(System.IO.Path.GetFileName(relativePath));
            return new FileNode(contentKey, location, metadata, cert);
        }

        [Fact]
        public void BuildSubmissionJson_SingleCert_ProducesSingleBatch()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.dll", cert), RootDir + "/bin/b.dll"),
            };

            var json = provider.BuildSubmissionJson(files);
            var doc = JsonDocument.Parse(json);

            doc.RootElement.GetProperty("Version").GetString().Should().Be("1.0.0");
            var batches = doc.RootElement.GetProperty("SignBatches");
            batches.GetArrayLength().Should().Be(1);

            var batch = batches[0];
            batch.GetProperty("SourceLocationType").GetString().Should().Be("UNC");
            batch.GetProperty("SignRequestFiles").GetArrayLength().Should().Be(2);

            var ops = batch.GetProperty("SigningInfo").GetProperty("Operations");
            ops.GetArrayLength().Should().Be(1);
            ops[0].GetProperty("KeyCode").GetString().Should().Be("CP-230012");
        }

        [Fact]
        public void BuildSubmissionJson_MultipleCerts_ProducesMultipleBatches()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var certA = CreateCert("CertA", "CP-111");
            var certB = CreateCert("CertB", "CP-222");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", certA), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.exe", certB), RootDir + "/bin/b.exe"),
                (CreateFileNode("bin/c.dll", certA), RootDir + "/bin/c.dll"),
            };

            var json = provider.BuildSubmissionJson(files);
            var doc = JsonDocument.Parse(json);

            var batches = doc.RootElement.GetProperty("SignBatches");
            batches.GetArrayLength().Should().Be(2);

            // CertA batch should have 2 files
            var batchA = batches[0];
            batchA.GetProperty("SignRequestFiles").GetArrayLength().Should().Be(2);
            batchA.GetProperty("SigningInfo").GetProperty("Operations")[0]
                .GetProperty("KeyCode").GetString().Should().Be("CP-111");

            // CertB batch should have 1 file
            var batchB = batches[1];
            batchB.GetProperty("SignRequestFiles").GetArrayLength().Should().Be(1);
            batchB.GetProperty("SigningInfo").GetProperty("Operations")[0]
                .GetProperty("KeyCode").GetString().Should().Be("CP-222");
        }

        [Fact]
        public void BuildPatternFile_ProducesCommaSeparatedRelativePaths()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
                (CreateFileNode("lib/b.dll", cert), RootDir + "/lib/b.dll"),
            };

            var pattern = provider.BuildPatternFile(files);

            pattern.Should().Be("bin/a.dll,lib/b.dll");
        }

        [Fact]
        public void BuildArguments_ContainsExpectedFlags()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);

            var args = provider.BuildArguments("/work");

            args.Should().Contain("-x regularSigning");
            args.Should().Contain("-y \"inlineSignParams\"");
            args.Should().Contain("-c 400");
            args.Should().Contain("-t 30");
            args.Should().Contain("-v \"Tls12\"");
            args.Should().Contain("-s \"https://api.esrp.microsoft.com/api/v2\"");
            args.Should().Contain("-o \"Microsoft\"");
            args.Should().Contain("-a test-client-id");
            args.Should().Contain("-d test-tenant-id");
            args.Should().Contain("esrpcli.dll");
            args.Should().Contain("-useMSIAuthentication false");
            args.Should().Contain("-encryptedCertificateData");
        }

        [Fact]
        public async Task SignFilesAsync_DryRun_ReturnsTrue_DoesNotInvokeProcess()
        {
            var config = CreateConfig(dryRun: true);
            var processRunner = new FakeProcessRunner();
            var provider = new ESRPCliSigningProvider(config, processRunner, NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
            };

            var result = await provider.SignFilesAsync(files);

            result.Should().BeTrue();
            processRunner.Invocations.Should().BeEmpty();
        }

        [Fact]
        public async Task SignFilesAsync_EmptyFiles_ReturnsTrue()
        {
            var config = CreateConfig();
            var processRunner = new FakeProcessRunner();
            var provider = new ESRPCliSigningProvider(config, processRunner, NullLogger<ESRPCliSigningProvider>.Instance);

            var result = await provider.SignFilesAsync(new List<(FileNode, string)>());

            result.Should().BeTrue();
            processRunner.Invocations.Should().BeEmpty();
        }

        [Fact]
        public void BuildSubmissionJson_FilesInDifferentTrees_ComputesCommonRoot()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA");

            var path1 = (RootDir + "/bin/a.dll").Replace('\\', '/');
            var path2 = (RootDir + "/lib/b.dll").Replace('\\', '/');
            var contentKey1 = new FileContentKey(
                new ContentHash(ImmutableArray.Create<byte>(1, 2, 3, 4)), "a.dll");
            var contentKey2 = new FileContentKey(
                new ContentHash(ImmutableArray.Create<byte>(5, 6, 7, 8)), "b.dll");
            var node1 = new FileNode(contentKey1, new FileLocation(path1, null), new FileMetadata("a.dll"), cert);
            var node2 = new FileNode(contentKey2, new FileLocation(path2, null), new FileMetadata("b.dll"), cert);

            var files = new List<(FileNode, string)> { (node1, path1), (node2, path2) };

            var json = provider.BuildSubmissionJson(files);
            var doc = JsonDocument.Parse(json);
            var batch = doc.RootElement.GetProperty("SignBatches")[0];

            // Common root should be the RootDir itself
            var sourceRoot = batch.GetProperty("SourceRootDirectory").GetString();
            sourceRoot.Should().Be(RootDir.Replace('\\', '/'));
        }

        [Fact]
        public void BuildSubmissionJson_FileRequestHasCorrelationId()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
            };

            var json = provider.BuildSubmissionJson(files);
            var doc = JsonDocument.Parse(json);
            var fileEntry = doc.RootElement.GetProperty("SignBatches")[0]
                .GetProperty("SignRequestFiles")[0];

            fileEntry.TryGetProperty("CustomerCorrelationId", out var corrId).Should().BeTrue();
            Guid.TryParse(corrId.GetString(), out _).Should().BeTrue();
        }

        /// <summary>
        /// Fake process runner that records invocations without running any real process.
        /// </summary>
        private sealed class FakeProcessRunner : IProcessRunner
        {
            public List<(string FileName, string Arguments)> Invocations { get; } = new();

            public ProcessResult NextResult { get; set; } = new ProcessResult(0, "Success\n", "");

            public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
            {
                Invocations.Add((fileName, arguments));
                return Task.FromResult(NextResult);
            }
        }
    }
}

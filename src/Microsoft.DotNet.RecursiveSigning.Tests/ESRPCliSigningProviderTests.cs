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
using AwesomeAssertions;
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
        public void GroupFilesByCertificate_SingleCert_ProducesSingleGroup()
        {
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.dll", cert), RootDir + "/bin/b.dll"),
            };

            var groups = ESRPCliSigningProvider.GroupFilesByCertificate(files);

            groups.Should().HaveCount(1);
            groups["CertA"].files.Should().HaveCount(2);
        }

        [Fact]
        public void GroupFilesByCertificate_MultipleCerts_ProducesMultipleGroups()
        {
            var certA = CreateCert("CertA", "CP-111");
            var certB = CreateCert("CertB", "CP-222");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", certA), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.exe", certB), RootDir + "/bin/b.exe"),
                (CreateFileNode("bin/c.dll", certA), RootDir + "/bin/c.dll"),
            };

            var groups = ESRPCliSigningProvider.GroupFilesByCertificate(files);

            groups.Should().HaveCount(2);
            groups["CertA"].files.Should().HaveCount(2);
            groups["CertB"].files.Should().HaveCount(1);
        }

        [Fact]
        public void BuildOperationsJson_ProducesCorrectJsonArray()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);
            var cert = CreateCert("CertA", "CP-230012");

            var json = provider.BuildOperationsJson(cert);
            var doc = JsonDocument.Parse(json);

            doc.RootElement.GetArrayLength().Should().Be(1);
            var op = doc.RootElement[0];
            op.GetProperty("KeyCode").GetString().Should().Be("CP-230012");
            op.GetProperty("OperationCode").GetString().Should().Be("SigntoolSign");
            op.GetProperty("ToolName").GetString().Should().Be("sign");
        }

        [Fact]
        public void BuildPatternFileContent_ProducesCommaSeparatedRelativePaths()
        {
            var cert = CreateCert("CertA");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", cert), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.dll", cert), RootDir + "/bin/b.dll"),
            };
            var rootDir = RootDir.Replace('\\', '/') + "/bin";

            var pattern = ESRPCliSigningProvider.BuildPatternFileContent(files, rootDir);

            pattern.Should().Be("a.dll,b.dll");
        }

        [Fact]
        public void BuildArguments_ContainsExpectedFlags()
        {
            var config = CreateConfig();
            var provider = new ESRPCliSigningProvider(config, new FakeProcessRunner(), NullLogger<ESRPCliSigningProvider>.Instance);

            var args = provider.BuildArguments("/work", RootDir);

            args.Should().Contain("-x regularSigning");
            args.Should().Contain("-y \"inlineSignParams\"");
            args.Should().Contain("-j");
            args.Should().Contain("-p");
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
        public void ExtractOperations_ReturnsOperationsArray()
        {
            var cert = CreateCert("CertA", "CP-230012");
            var ops = ESRPCliSigningProvider.ExtractOperations(cert.CertificateDefinition);

            ops.GetArrayLength().Should().Be(1);
            ops[0].GetProperty("KeyCode").GetString().Should().Be("CP-230012");
        }

        [Fact]
        public async Task SignFilesAsync_MultipleCerts_InvokesProcessPerGroup()
        {
            var config = CreateConfig();
            var processRunner = new FakeProcessRunner();
            var provider = new ESRPCliSigningProvider(config, processRunner, NullLogger<ESRPCliSigningProvider>.Instance);
            var certA = CreateCert("CertA", "CP-111");
            var certB = CreateCert("CertB", "CP-222");
            var files = new List<(FileNode, string)>
            {
                (CreateFileNode("bin/a.dll", certA), RootDir + "/bin/a.dll"),
                (CreateFileNode("bin/b.exe", certB), RootDir + "/bin/b.exe"),
            };

            var result = await provider.SignFilesAsync(files);

            result.Should().BeTrue();
            // One invocation per cert group, submitted in parallel
            processRunner.Invocations.Should().HaveCount(2);
        }

        /// <summary>
        /// Fake process runner that records invocations without running any real process.
        /// Thread-safe for parallel invocation testing.
        /// </summary>
        private sealed class FakeProcessRunner : IProcessRunner
        {
            private readonly List<(string FileName, string Arguments)> _invocations = new();

            public IReadOnlyList<(string FileName, string Arguments)> Invocations
            {
                get { lock (_invocations) { return _invocations.ToList(); } }
            }

            public ProcessResult NextResult { get; set; } = new ProcessResult(0, "Success\n", "");

            public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
            {
                lock (_invocations)
                {
                    _invocations.Add((fileName, arguments));
                }
                return Task.FromResult(NextResult);
            }
        }
    }
}

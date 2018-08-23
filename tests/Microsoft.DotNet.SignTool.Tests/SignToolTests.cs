// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignToolTests
    {
        private readonly string _microbuildPath;
        private readonly string _publishURL;
        private readonly bool _testSign;
        private readonly bool _isWindows;
        private readonly SignToolTask _task;
        private readonly SignToolArgs _signToolArgs;
        private readonly SignTool _signTool;

        public SignToolTests()
        {
            _microbuildPath = string.Empty;
            _publishURL = null;
            _testSign = true;

            // As of now we don't have "mscoree.dll" on Linux. This DLL is used when checking
            // if the file is strong name signed: SignTool/ContentUtil.NativeMethods
            // Therefore, test cases won't execute in fully on non-Windows machines.
            _isWindows = System.Environment.OSVersion.VersionString.Contains("Windows");

            var testBasePath = Guid.NewGuid().ToString();
            var tempPath = $@"{testBasePath}/TestTempDir/";
            var logDir = $@"{testBasePath}/TestLogDir/";

            // The path to MSBuild will always be null in these tests, this will force
            // the signing logic to call our FakeBuildEngine.BuildProjectFile with a path
            // to the XML that store the content of the would be Microbuild sign request.
            _signToolArgs = new SignToolArgs(tempPath, _microbuildPath, _testSign, null, logDir);

            _signTool = new ValidationOnlySignTool(_signToolArgs);

            _task = new SignToolTask
            {
                BuildEngine = new FakeBuildEngine()
            };
        }

        private void TestCaseEpilogue(string[] itemsToSign, Dictionary<string, SignInfo> strongNameSignInfo, 
            Dictionary<ExplicitCertificateKey, string> signingOverridingInfos, List<FileName> expectedToBeSigned)
        {
            if (!_isWindows) return;

            var signingInput = new Configuration(_signToolArgs.TempDir, itemsToSign, strongNameSignInfo, signingOverridingInfos, _publishURL, _task.Log).GenerateListOfFiles();

            Assert.Equal(expectedToBeSigned.Count, signingInput.FilesToSign.Count());

            // Check that all files that were expected to be discovered were actually found and the 
            // signing information for them are correct.
            var signInfoCheck = signingInput.FilesToSign.All<FileName>(candidate =>
            {
                return expectedToBeSigned.Exists(expected =>
                    candidate.FullPath.EndsWith(expected.FullPath) &&
                    candidate.SignInfo.Certificate == expected.SignInfo.Certificate &&
                    candidate.SignInfo.StrongName == expected.SignInfo.StrongName);
            });
            Assert.True(signInfoCheck);
 
            var util = new BatchSignUtil(_task.BuildEngine, _task.Log, _signTool, signingInput, null);

            util.Go();

            // The list of files that would be signed was captured inside the FakeBuildEngine,
            // here we check if that matches what we expected
            var fakeEngine = (FakeBuildEngine)_task.BuildEngine;

            foreach (var expected in expectedToBeSigned)
            {
                var signedInfoCheck = fakeEngine.filesSigned.Exists(candidate =>
                    candidate.FullPath.EndsWith(expected.FullPath) &&
                    candidate.SignInfo.Certificate == expected.SignInfo.Certificate &&
                    candidate.SignInfo.StrongName == expected.SignInfo.StrongName);

                Assert.True(signedInfoCheck, $"Expected this file ({expected.FullPath}) to be signed with this " +
                        $"certificate ({expected.SignInfo.Certificate}) and this strong name ({expected.SignInfo.StrongName})");
            }

            Assert.False(_task.Log.HasLoggedErrors);
        }

        [Fact]
        public void EmptySigningList()
        {
            var ExplicitSignItems = new string[1];

            var StrongNameSignInfo = new Dictionary<string, SignInfo>();

            var FileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            var signingInput = new Configuration(_signToolArgs.TempDir, ExplicitSignItems, StrongNameSignInfo, FileSignInfo, _publishURL, _task.Log).GenerateListOfFiles();

            Assert.Empty(signingInput.FilesToSign);
            Assert.Empty(signingInput.ZipDataMap);
            Assert.False(_task.Log.HasLoggedErrors);
        }

        [Fact]
        public void OnlyContainer()
        {
            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/ContainerOne.1.0.0.nupkg",
            };

            // Default signing information
            var signingInformation = new Dictionary<string, SignInfo>() {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            var expectedAsmSignInfo = new SignInfo("ArcadeCertTest", "ArcadeStrongTest");
            var expectedNugSignInfo = new SignInfo(SignToolConstants.Certificate_NuGet, null);
            var expectedNatSignInfo = new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2, null);
            var expectedSigningList = new List<FileName>()
            {
                new FileName("/ContainerOne.1.0.0.nupkg", expectedNugSignInfo),
                new FileName("/netcoreapp2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.0/ContainerOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.1/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netstandard2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/net461/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/native/NativeLibrary.dll", expectedNatSignInfo),
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>();

            TestCaseEpilogue(itemsToSign, signingInformation, signingOverridingInformation, expectedSigningList);
        }

        [Fact]
        public void OnlyContainerAndOverriding()
        {
            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/ContainerOne.1.0.0.nupkg",
            };

            // Default signing information
            var signingInformation = new Dictionary<string, SignInfo>() {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>() {
                {new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c", SignToolConstants.AllTargetFrameworksSentinel), "OverridedCertName" }
            };

            var overridingCert = new SignInfo("OverridedCertName", "ArcadeStrongTest");

            var expectedAsmSignInfo = new SignInfo("ArcadeCertTest", "ArcadeStrongTest");
            var expectedNugSignInfo = new SignInfo(SignToolConstants.Certificate_NuGet, null);
            var expectedNatSignInfo = new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2, null);
            var expectedSigningList = new List<FileName>()
            {
                new FileName("/ContainerOne.1.0.0.nupkg", expectedNugSignInfo),
                new FileName("/netcoreapp2.0/ProjectOne.dll", overridingCert),
                new FileName("/netcoreapp2.0/ContainerOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.1/ProjectOne.dll", overridingCert),
                new FileName("/netstandard2.0/ProjectOne.dll", overridingCert),
                new FileName("/net461/ProjectOne.dll", overridingCert),
                new FileName("/native/NativeLibrary.dll", expectedNatSignInfo),
            };

            TestCaseEpilogue(itemsToSign, signingInformation, signingOverridingInformation, expectedSigningList);
        }

        [Fact]
        public void EmptyPKT()
        {
            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/EmptyPKT.dll",
            };

            // Default signing information
            var signingInformation = new Dictionary<string, SignInfo>() {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>() {
                {new ExplicitCertificateKey("EmptyPKT.dll", "", SignToolConstants.AllTargetFrameworksSentinel), "OverridedCertName" }
            };

            var expectedSigningList = new List<FileName>()
            {
                new FileName("EmptyPKT.dll", new SignInfo("OverridedCertName", "")),
            };

            TestCaseEpilogue(itemsToSign, signingInformation, signingOverridingInformation, expectedSigningList);
        }


        [Fact]
        public void NestedContainer()
        {
            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/NestedContainer.1.0.0.nupkg",
            };

            // Default signing information
            var signingInformation = new Dictionary<string, SignInfo>() {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            var expectedAsmSignInfo = new SignInfo("ArcadeCertTest", "ArcadeStrongTest");
            var expectedNugSignInfo = new SignInfo(SignToolConstants.Certificate_NuGet, null);
            var expectedNatSignInfo = new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2, null);
            var expectedSigningList = new List<FileName>()
            {
                new FileName("/NestedContainer.1.0.0.nupkg", expectedNugSignInfo),
                new FileName("ContainerOne.1.0.0.nupkg", expectedNugSignInfo),
                new FileName("/netcoreapp2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.0/ContainerOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.0/ContainerTwo.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.1/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netcoreapp2.1/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netstandard2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/netstandard2.0/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/net461/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/net461/ProjectOne.dll", expectedAsmSignInfo),
                new FileName("/native/NativeLibrary.dll", expectedNatSignInfo),
                new FileName("/native/NativeLibrary.dll", expectedNatSignInfo),
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>();

            TestCaseEpilogue(itemsToSign, signingInformation, signingOverridingInformation, expectedSigningList);
        }

        [Fact]
        public void AcceptAJarFile()
        {
            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/Sample.jar",
            };

            // Default signing information
            var signingInformation = new Dictionary<string, SignInfo>();

            var signInfo = new SignInfo("CP-232612-Java", string.Empty);

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>()
            {
                {new ExplicitCertificateKey("Sample.jar", string.Empty, SignToolConstants.AllTargetFrameworksSentinel), signInfo.Certificate }
            };

            var expectedSigningList = new List<FileName>()
            {
                new FileName("Sample.jar", signInfo),
            };

            TestCaseEpilogue(itemsToSign, signingInformation, signingOverridingInformation, expectedSigningList);
        }
    }
}

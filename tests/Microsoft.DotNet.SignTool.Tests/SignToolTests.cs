// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignToolTests
    {
        private string _MicrobuildPath { get; }
        private string _MSBuildPath { get; }
        private string _PublishURL { get; }
        private bool _TestSign { get; }
        private bool _DryRun { get; }
        private bool _IsWindows { get; }

        public SignToolTests()
        {
            var pathAttributes = Assembly.GetExecutingAssembly().GetCustomAttribute<PathConfiguration>();

            _MicrobuildPath = pathAttributes.PackageInstallationPath;
            _MSBuildPath = pathAttributes.MSBuildPath;
            _PublishURL = null;
            _TestSign = true;
            _DryRun = String.IsNullOrWhiteSpace(_MSBuildPath);

            // As of now we don't have "mscoree.dll" on Linux. This DLL is used when checking
            // if the file is strong name signed: SignTool/ContentUtil.NativeMethods
            // Therefore, test cases won't execute in fully on non-Windows machines.
            _IsWindows = System.Environment.OSVersion.VersionString.Contains("Windows");
        }

        private (SignToolTask, SignToolArgs, SignTool) TestCasePrologue()
        {
            var TestBasePath = Guid.NewGuid().ToString();
            var TempPath = $@"{TestBasePath}/TestTempDir/";
            var LogDir = $@"{TestBasePath}/TestLogDir/";

            var signToolArgs = new SignToolArgs(TempPath, _MicrobuildPath, _TestSign, _MSBuildPath, LogDir);

            var signTool = _DryRun ? new ValidationOnlySignTool(signToolArgs) : (SignTool)new RealSignTool(signToolArgs);

            var task = new SignToolTask
            {
                BuildEngine = new FakeBuildEngine()
            };

            return (task, signToolArgs, signTool);
        }

        private void TestCaseEpilogue(SignToolTask task, SignTool signTool, SignToolArgs signToolArgs, string[] itemsToSign, 
            Dictionary<string, SignInfo> strongNameSignInfo, Dictionary<(string, string, string), string> signingOverridingInfos,
            List<FileName> expectedToBeSigned)
        {
            if (!_IsWindows) return;

            var signingInput = new BatchSignInput(signToolArgs.TempDir, itemsToSign, strongNameSignInfo, signingOverridingInfos, _PublishURL, task.Log);

            /// Check that all files that were expected to be sent to signing were actually found and the 
            /// signing information for them are correct.
            foreach (var expected in expectedToBeSigned)
            {
                if (!signingInput.FilesToSign.Exists(candidate => candidate.FullPath.EndsWith(expected.FullPath) && 
                    candidate.SignInfo.Certificate == expected.SignInfo.Certificate && 
                    candidate.SignInfo.StrongName == expected.SignInfo.StrongName))
                {
                    task.Log.LogError($"Expected this file ({expected.FullPath}) to be signed with this " +
                        $"certificate ({expected.SignInfo.Certificate}) and this strong name ({expected.SignInfo.StrongName})");
                }
            }

            if (expectedToBeSigned.Count != signingInput.FilesToSign.Count)
            {
                task.Log.LogError($"Expected a signing list of {expectedToBeSigned.Count} items but got one with {signingInput.FilesToSign.Count} items.");
            }
 
            var util = new BatchSignUtil(task.BuildEngine, task.Log, signTool, signingInput, null);

            /// There is a validation inside this method that checks that the files where actually signed
            /// so it's not duplicated here.
            util.Go();

            if (task.Log.HasLoggedErrors)
            {
                foreach (var item in ((FakeBuildEngine)task.BuildEngine).LogErrorEvents)
                {
                    Console.WriteLine(item.Message);
                }
            }

            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void EmptySigningList()
        {
            (var task, var signToolArgs, _) = TestCasePrologue();

            var ExplicitSignItems = new string[1];

            var StrongNameSignInfo = new Dictionary<string, SignInfo>();

            var FileSignInfo = new Dictionary<(string, string, string), string>();

            var signingInput = new BatchSignInput(signToolArgs.TempDir, ExplicitSignItems, StrongNameSignInfo, FileSignInfo, _PublishURL, task.Log);

            Assert.Empty(signingInput.FilesToSign);
            Assert.Empty(signingInput.ZipDataMap);
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void OnlyContainer()
        {
            (var task, var signToolArgs, var signTool) = TestCasePrologue();

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
            var signingOverridingINformation = new Dictionary<(string, string, string), string>();

            TestCaseEpilogue(task, signTool, signToolArgs, itemsToSign, signingInformation, signingOverridingINformation, expectedSigningList);
        }

        [Fact]
        public void NestedContainer()
        {
            (var task, var signToolArgs, var signTool) = TestCasePrologue();

            // List of files to be considered for signing
            var itemsToSign = new string[] {
                $@"Resources/ContainerTwo.1.0.0.nupkg",
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
                new FileName("ContainerTwo.1.0.0.nupkg", expectedNugSignInfo),
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
            var signingOverridingINformation = new Dictionary<(string, string, string), string>();

            TestCaseEpilogue(task, signTool, signToolArgs, itemsToSign, signingInformation, signingOverridingINformation, expectedSigningList);
        }
    }
}

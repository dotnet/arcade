// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignToolTests : IDisposable
    {
        private readonly string _tmpDir;
        private readonly ITestOutputHelper _output;

        // Default extension based signing information
        private static readonly Dictionary<string, SignInfo> s_fileExtensionSignInfo = new Dictionary<string, SignInfo>()
        {
            {".js", new SignInfo("JSCertificate") },
            {".jar", new SignInfo("JARCertificate") },
            {".ps1", new SignInfo("PSCertificate") },
            {".psd1", new SignInfo("PSDCertificate") },
            {".psm1", new SignInfo("PSMCertificate") },
            {".psc1", new SignInfo("PSCCertificate") },
            {".dylib", new SignInfo("DylibCertificate") },
            {".dll", new SignInfo("Microsoft400") },
            {".exe", new SignInfo("Microsoft400") },
            {".vsix", new SignInfo("VsixSHA2") },
            {".zip", SignInfo.Ignore },
            {".nupkg", new SignInfo("NuGet") },
        };

        // Default extension based signing information post build
        private static readonly ITaskItem[] s_fileExtensionSignInfoPostBuild = new ITaskItem[]
        {
            new TaskItem(".js", new Dictionary<string, string> { 
                { "CertificateName", "JSCertificate" },
                {"BARBuildID", "123" } 
            }),
            new TaskItem(".jar", new Dictionary<string, string> {
                { "CertificateName", "JARCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".ps1", new Dictionary<string, string> {
                { "CertificateName", "PSCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".psd1", new Dictionary<string, string> {
                { "CertificateName", "PSDCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".psm1", new Dictionary<string, string> {
                { "CertificateName", "PSMCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".psc1", new Dictionary<string, string> {
                { "CertificateName", "PSCCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".psd1", new Dictionary<string, string> {
                { "CertificateName", "PSDCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".dylib", new Dictionary<string, string> {
                { "CertificateName", "DylibCertificate" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".dll", new Dictionary<string, string> {
                { "CertificateName", "Microsoft400" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".exe", new Dictionary<string, string> {
                { "CertificateName", "Microsoft400" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".zip", new Dictionary<string, string> {
                { "CertificateName", "" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".nupkg", new Dictionary<string, string> {
                { "CertificateName", "NuGet" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".vsix", new Dictionary<string, string> {
                { "CertificateName", "VsixSHA2" },
                {"BARBuildID", "123" }
            }),
            new TaskItem(".js", new Dictionary<string, string> {
                { "CertificateName", "JSCertificate" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".jar", new Dictionary<string, string> {
                { "CertificateName", "JARCertificate" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".ps1", new Dictionary<string, string> {
                { "CertificateName", "PSCertificate" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".psd1", new Dictionary<string, string> {
                { "CertificateName", "PSDCertificate" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".dll", new Dictionary<string, string> {
                { "CertificateName", "Microsoft400" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".nupkg", new Dictionary<string, string> {
                { "CertificateName", "NuGet" },
                {"BARBuildID", "234" }
            }),
            new TaskItem(".vsix", new Dictionary<string, string> {
                { "CertificateName", "VsixSHA2" },
                {"BARBuildID", "234" }
            })
        };

        /// <summary>
        /// List of known signable extensions. Copied, removing duplicates, from here:
        /// https://microsoft.sharepoint.com/teams/codesigninfo/Wiki/Signable%20Files.aspx
        /// </summary>
        public static readonly string[] SignableExtensions =
        {
            ".exe",
            ".dll",
            ".rll",
            ".olb",
            ".ocx",

            ".cab",

            ".cat",

            ".vbs",
            ".js",
            ".wfs",

            ".msi",
            ".mui",
            ".msp",
            ".msu",
            ".psf",
            ".mpb",
            ".mp",
            ".msm",

            ".doc",
            ".xls",
            ".ppt",
            ".xla",
            ".vdx",
            ".xsn",
            ".mpp",

            ".xlam",
            ".xlsb",
            ".xlsm",
            ".xltm",
            ".potm",
            ".ppsm",
            ".pptm",
            ".docm",
            ".dotm",

            ".ttf",
            ".otf",

            ".ps1",
            ".ps1xml",
            ".psm1",
            ".psd1",
            ".psc1",
            ".cdxml",
            ".wsf",
            ".mof",

            ".sft",
            ".dsft",

            ".vsi",

            ".xap",

            ".efi",

            ".vsix",

            ".jar",

            ".winmd",

            ".appx",
            ".appxbundle",

            ".esd",

            ".py",
            ".pyd"
        };

        public static IEnumerable<object[]> GetSignableExtensions()
        {
            foreach (var extension in SignableExtensions)
            {
                yield return new object[] { extension };
            }
        }

        public SignToolTests(ITestOutputHelper output)
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tmpDir);
            _output = output;
        }

        private string GetResourcePath(string name, string relativePath = null)
        {
            var srcPath = Path.Combine(Path.GetDirectoryName(typeof(SignToolTests).Assembly.Location), "Resources", name);

            var dstDir = _tmpDir;

            if (relativePath != null)
            {
                dstDir = Path.Combine(dstDir, relativePath);
                Directory.CreateDirectory(dstDir);
            }

            var dstPath = Path.Combine(dstDir, name);

            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath);
            }

            return dstPath;
        }

        private string CreateTestResource(string name)
        {
            var dstPath = Path.Combine(_tmpDir, name);

            File.WriteAllText(dstPath, "This is a test file content");

            return dstPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tmpDir, recursive: true);
            }
            catch
            {
            }
        }

        private void ValidateGeneratedProject(
            string[] itemsToSign,
            Dictionary<string, SignInfo> strongNameSignInfo,
            Dictionary<ExplicitCertificateKey, string> fileSignInfo,
            Dictionary<string, SignInfo> extensionsSignInfo,
            string[] expectedXmlElementsPerSingingRound,
            string[] dualCertificates = null)
        {
            var buildEngine = new FakeBuildEngine();

            var task = new SignToolTask { BuildEngine = buildEngine };

            // The path to MSBuild will always be null in these tests, this will force
            // the signing logic to call our FakeBuildEngine.BuildProjectFile with a path
            // to the XML that store the content of the would be Microbuild sign request.
            var signToolArgs = new SignToolArgs(_tmpDir, microBuildCorePath: "MicroBuildCorePath", testSign: true, msBuildPath: null, _tmpDir, enclosingDir: "", "");

            var signTool = new FakeSignTool(signToolArgs, task.Log);
            var signingInput = new Configuration(signToolArgs.TempDir, itemsToSign, strongNameSignInfo, fileSignInfo, extensionsSignInfo, dualCertificates, task.Log).GenerateListOfFiles();
            var util = new BatchSignUtil(task.BuildEngine, task.Log, signTool, signingInput, new string[] { });

            util.Go(doStrongNameCheck: true);

            Assert.Same(ByteSequenceComparer.Instance, signingInput.ZipDataMap.KeyComparer);

            // The list of files that would be signed was captured inside the FakeBuildEngine,
            // here we check if that matches what we expected
            var actualXmlElementsPerSingingRound = buildEngine.FilesToSign.Select(round => string.Join(Environment.NewLine, round));
            AssertEx.Equal(expectedXmlElementsPerSingingRound, actualXmlElementsPerSingingRound, comparer: AssertEx.EqualIgnoringWhitespace, itemInspector: s => s.Replace("\"", "\"\""));

            Assert.False(task.Log.HasLoggedErrors);
        }

        private void ValidateGeneratedProject(
            ITaskItem[] itemsToSign,
            ITaskItem[] strongNameSignInfo,
            ITaskItem[] fileSignInfo,
            ITaskItem[] extensionsSignInfo,
            ITaskItem[] certificatesSignInfo,
            string[] expectedXmlElementsPerSingingRound)
        {
            var buildEngine = new FakeBuildEngine();
            var task = new SignToolTask { BuildEngine = buildEngine };

            // The path to MSBuild will always be null in these tests, this will force
            // the signing logic to call our FakeBuildEngine.BuildProjectFile with a path
            // to the XML that store the content of the would be Microbuild sign request.
            var signToolArgs = new SignToolArgs(
                _tmpDir,
                microBuildCorePath: "MicroBuildCorePath", 
                testSign: true, 
                msBuildPath: null, 
                _tmpDir, 
                enclosingDir: "", 
                "");

            var signTool = new FakeSignTool(signToolArgs, task.Log);
            var signingInput = new Configuration(
                signToolArgs.TempDir,
                itemsToSign,
                strongNameSignInfo,
                fileSignInfo,
                extensionsSignInfo,
                certificatesSignInfo,
                task.Log).GenerateListOfFiles();

            var util = new BatchSignUtil(task.BuildEngine, task.Log, signTool, signingInput, new string[] { });

            util.Go(doStrongNameCheck: true);

            Assert.Same(ByteSequenceComparer.Instance, signingInput.ZipDataMap.KeyComparer);

            // The list of files that would be signed was captured inside the FakeBuildEngine,
            // here we check if that matches what we expected
            var actualXmlElementsPerSingingRound = buildEngine.FilesToSign.Select(round => string.Join(Environment.NewLine, round));
            AssertEx.Equal(expectedXmlElementsPerSingingRound, actualXmlElementsPerSingingRound, comparer: AssertEx.EqualIgnoringWhitespace, itemInspector: s => s.Replace("\"", "\"\""));

            Assert.False(task.Log.HasLoggedErrors);
        }

        private void ValidateFileSignInfos(
            string[] itemsToSign,
            Dictionary<string, SignInfo> strongNameSignInfo,
            Dictionary<ExplicitCertificateKey, string> fileSignInfo,
            Dictionary<string, SignInfo> extensionsSignInfo,
            string[] expected,
            string[] expectedCopyFiles = null,
            string[] dualCertificates = null,
            string[] expectedErrors = null,
            string[] expectedWarnings = null)
        {
            var engine = new FakeBuildEngine();
            var task = new SignToolTask { BuildEngine = engine };
            var signingInput = new Configuration(_tmpDir, itemsToSign, strongNameSignInfo, fileSignInfo, extensionsSignInfo, dualCertificates, task.Log).GenerateListOfFiles();

            AssertEx.Equal(expected, signingInput.FilesToSign.Select(f => f.ToString()));
            AssertEx.Equal(expectedCopyFiles ?? Array.Empty<string>(), signingInput.FilesToCopy.Select(f => $"{f.Key} -> {f.Value}"));

            AssertEx.Equal(expectedErrors ?? Array.Empty<string>(), engine.LogErrorEvents.Select(w => w.Message));
            AssertEx.Equal(expectedWarnings ?? Array.Empty<string>(), engine.LogWarningEvents.Select(w => $"{w.Code}: {w.Message}"));
        }

        private void ValidateFileSignInfos(
            ITaskItem[] itemsToSign,
            ITaskItem[] strongNameSignInfo,
            ITaskItem[] fileSignInfo,
            ITaskItem[] extensionsSignInfo,
            ITaskItem[] certificatesSignInfo,
            string[] expected,
            string[] expectedCopyFiles = null,
            string[] expectedErrors = null,
            string[] expectedWarnings = null)
        {
            var engine = new FakeBuildEngine();
            var task = new SignToolTask { BuildEngine = engine };
            var signingInput = new Configuration(
                _tmpDir,
                itemsToSign,
                strongNameSignInfo,
                fileSignInfo,
                extensionsSignInfo,
                certificatesSignInfo,
                task.Log).GenerateListOfFiles();

            AssertEx.Equal(expected, signingInput.FilesToSign.Select(f => f.ToString()));
            AssertEx.Equal(expectedCopyFiles ?? Array.Empty<string>(), signingInput.FilesToCopy.Select(f => $"{f.Key} -> {f.Value}"));

            AssertEx.Equal(expectedErrors ?? Array.Empty<string>(), engine.LogErrorEvents.Select(w => w.Message));
            AssertEx.Equal(expectedWarnings ?? Array.Empty<string>(), engine.LogWarningEvents.Select(w => $"{w.Code}: {w.Message}"));
        }

        [Fact]
        public void EmptySigningList()
        {
            var itemsToSign = new string[0];

            var strongNameSignInfo = new Dictionary<string, SignInfo>();

            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };
            var signingInput = new Configuration(_tmpDir, itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, null, task.Log).GenerateListOfFiles();

            Assert.Empty(signingInput.FilesToSign);
            Assert.Empty(signingInput.ZipDataMap);
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void EmptySigningListPostBuild()
        {
            var itemsToSign = new ITaskItem[0];
            var strongNameSignInfo = new ITaskItem[0];
            var fileSignInfo = new ITaskItem[0];
            var certificatesSignInfo = new ITaskItem[0];

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };
            var signingInput = new Configuration(
                _tmpDir, 
                itemsToSign, 
                strongNameSignInfo,
                fileSignInfo,
                s_fileExtensionSignInfoPostBuild,
                certificatesSignInfo,
                task.Log).GenerateListOfFiles();

            Assert.Empty(signingInput.FilesToSign);
            Assert.Empty(signingInput.ZipDataMap);
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void EmptySigningListForTask()
        {
            var task = new SignToolTask {
                BuildEngine = new FakeBuildEngine(),
                ItemsToSign = Array.Empty<ITaskItem>(),
                StrongNameSignInfo = Array.Empty<ITaskItem>(),
                LogDir = "LogDir",
                TempDir = "TempDir",
                DryRun = false,
                TestSign = true,
                MSBuildPath = CreateTestResource("msbuild.fake"),
                SNBinaryPath = CreateTestResource("fake.sn.exe")
            };

            Assert.True(task.Execute());
        }

        [Fact]
        public void SignWhenSnExeIsNotRequired()
        {
            var task = new SignToolTask
            {
                BuildEngine = new FakeBuildEngine(_output),
                ItemsToSign = Array.Empty<ITaskItem>(),
                StrongNameSignInfo = Array.Empty<ITaskItem>(),
                LogDir = "LogDir",
                TempDir = "TempDir",
                DryRun = false,
                TestSign = true,
                MSBuildPath = CreateTestResource("msbuild.fake"),
                DoStrongNameCheck = false,
                SNBinaryPath = null,
            };

            Assert.True(task.Execute());
        }

        [Fact]
        public void OnlyContainer()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("ContainerOne.1.0.0.nupkg"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'",
            });
        }

        [Fact]
        public void OnlyContainerPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("ContainerOne.1.0.0.nupkg"), new Dictionary<string, string> 
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "123" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo,
                new ITaskItem[0],
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'",
            });
        }

        [Fact]
        public void SkipSigning()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("ContainerOne.1.0.0.nupkg"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>
            {
                { new ExplicitCertificateKey("NativeLibrary.dll"), "None" },
                { new ExplicitCertificateKey("ProjectOne.dll", publicKeyToken: "581d91ccdfc4ea9c", targetFramework: ".NETCoreApp,Version=v2.1"), "None" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            });
        }

        [Fact]
        public void SkipSigningPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("ContainerOne.1.0.0.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("NativeLibrary.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "None" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "TargetFramework", ".NETCoreApp,Version=v2.1" },
                    { "CertificateName", "None" },
                    { "BARBuildID", "123" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign,
                strongNameSignInfo,
                fileSignInfo,
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            });
        }

        [Fact]
        public void OnlyAuthenticodeSignByPKT()
        {
            var fileToTest = "ProjectOne.dll";
            var pktToTest = "581d91ccdfc4ea9c";
            var certificateToTest = "3PartySHA2";

            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath(fileToTest)
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { pktToTest, new SignInfo(certificateToTest) }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, new Dictionary<string, SignInfo>(), new[]
            {
                $"File '{fileToTest}' TargetFramework='.NETStandard,Version=v2.0' Certificate='{certificateToTest}'",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, new Dictionary<string, SignInfo>(), new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, fileToTest)}"">
  <Authenticode>{certificateToTest}</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void OnlyAuthenticodeSignByPKTPostBuild()
        {
            var fileToTest = "ProjectOne.dll";
            var pktToTest = "581d91ccdfc4ea9c";
            var certificateToTest = "3PartySHA2";

            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath(fileToTest), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("", new Dictionary<string, string>
                {
                    { "PublicKeyToken", pktToTest },
                    { "CertificateName", certificateToTest },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign,
                strongNameSignInfo,
                fileSignInfo,
                new ITaskItem[0],
                new ITaskItem[0],
                new[]
            {
                $"File '{fileToTest}' TargetFramework='.NETStandard,Version=v2.0' Certificate='{certificateToTest}' StrongName=''",
            });

            ValidateGeneratedProject(
                itemsToSign,
                strongNameSignInfo,
                fileSignInfo,
                new ITaskItem[0],
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, fileToTest)}"">
  <Authenticode>{certificateToTest}</Authenticode>
  <StrongName></StrongName>
</FilesToSign>
"
            });
        }


        [Fact]
        public void OnlyContainerAndOverridingByPKT()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("ContainerOne.1.0.0.nupkg"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c"), "OverriddenCertificate" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            },
            expectedWarnings: new[]
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "3", "lib/net461/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "5", "lib/netcoreapp2.0/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "6", "lib/netcoreapp2.1/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/netstandard2.0/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void OnlyContainerAndOverridingByPKTPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("ContainerOne.1.0.0.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "OverriddenCertificate" },
                    { "BARBuildID", "123" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign,
                strongNameSignInfo, 
                fileSignInfo,
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            },
            expectedWarnings: new[]
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "3", "lib/net461/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "5", "lib/netcoreapp2.0/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "6", "lib/netcoreapp2.1/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/netstandard2.0/ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void OnlyContainerAndOverridingByFileName()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("ContainerOne.1.0.0.nupkg"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("NativeLibrary.dll"), "OverriddenCertificate1" },
                { new ExplicitCertificateKey("ProjectOne.dll"), "3PartySHA2" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='OverriddenCertificate1'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            }, 
            expectedWarnings: new[] 
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "4", "lib/netcoreapp2.0/ContainerOne.dll")}' with Microsoft certificate 'ArcadeCertTest'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void OnlyContainerAndOverridingByFileNamePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("ContainerOne.1.0.0.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "ArcadeCertTest" },
                    { "BARBuildID", "234" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("NativeLibrary.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "OverriddenCertificate1" },
                    { "BARBuildID", "234" }
                }),
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "234" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='OverriddenCertificate1'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
            },
            expectedWarnings: new[]
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "4", "lib/netcoreapp2.0/ContainerOne.dll")}' with Microsoft certificate 'ArcadeCertTest'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void EmptyPKT()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("EmptyPKT.dll")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("EmptyPKT.dll"), "3PartySHA2" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2'",
            });
        }

        [Fact]
        public void EmptyPKTPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("EmptyPKT.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "CertificateName", "ArcadeCertTest" },
                    { "BARBuildID", "234" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("EmptyPKT.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "234" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2'",
            });
        }

        [Fact]
        public void CrossGenerated()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("CoreLibCrossARM.dll"),
                GetResourcePath("AspNetCoreCrossLib.dll")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "7cec85d7bea7798e", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") },
                { "adb9793829ddae60", new SignInfo("Microsoft400", "AspNetCore") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("EmptyPKT.dll"), "3PartySHA2" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, new Dictionary<string, SignInfo>(), new[]
            {
                "File 'CoreLibCrossARM.dll' Certificate='ArcadeCertTest'",
                "File 'AspNetCoreCrossLib.dll' TargetFramework='.NETCoreApp,Version=v3.0' Certificate='Microsoft400'",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, new Dictionary<string, SignInfo>(), new[]
            {
$@"<FilesToSign Include=""{Path.Combine(_tmpDir, "CoreLibCrossARM.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "AspNetCoreCrossLib.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>",
            });
        }

        [Fact]
        public void CrossGeneratedPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("CoreLibCrossARM.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                }),
                new TaskItem(GetResourcePath("AspNetCoreCrossLib.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "7cec85d7bea7798e" },
                    { "CertificateName", "ArcadeCertTest" },
                    { "BARBuildID", "234" }
                }),
                new TaskItem("AspNetCore", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "adb9793829ddae60" },
                    { "CertificateName", "Microsoft400" },
                    { "BARBuildID", "234" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("EmptyPKT.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "234" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo,
                new ITaskItem[0],
                new ITaskItem[0],
                new[]
            {
                "File 'CoreLibCrossARM.dll' Certificate='ArcadeCertTest'",
                "File 'AspNetCoreCrossLib.dll' TargetFramework='.NETCoreApp,Version=v3.0' Certificate='Microsoft400'",
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo,
                new ITaskItem[0],
                new ITaskItem[0],
                new[]
            {
$@"<FilesToSign Include=""{Path.Combine(_tmpDir, "CoreLibCrossARM.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "AspNetCoreCrossLib.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>",
            });
        }

        [Fact]
        public void DefaultCertificateForAssemblyWithoutStrongName()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("EmptyPKT.dll")
            };

            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "", new SignInfo("3PartySHA2") }
            };

            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>() { };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2'",
            });
        }

        [Fact]
        public void DefaultCertificateForAssemblyWithoutStrongNamePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("EmptyPKT.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "BARBuildID", "234" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName=''",
            });
        }

        [Fact]
        public void CustomTargetFrameworkAttribute()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("CustomTargetFrameworkAttribute.dll")
            };

            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                {  "", new SignInfo("DefaultCertificate") }
            };

            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("CustomTargetFrameworkAttribute.dll", targetFramework: ".NETFramework,Version=v2.0"), "3PartySHA2" }
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'CustomTargetFrameworkAttribute.dll' TargetFramework='.NETFramework,Version=v2.0' Certificate='3PartySHA2'",
            });
        }

        [Fact]
        public void CustomTargetFrameworkAttributePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("CustomTargetFrameworkAttribute.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "234" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("", new Dictionary<string, string>
                {
                    { "CertificateName", "DefaultCertificate" },
                    { "BARBuildID", "234" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("CustomTargetFrameworkAttribute.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "TargetFramework", ".NETFramework,Version=v2.0" },
                    { "BARBuildID", "234" }
                })
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'CustomTargetFrameworkAttribute.dll' TargetFramework='.NETFramework,Version=v2.0' Certificate='3PartySHA2' StrongName=''",
            });
        }

        [Fact]
        public void ThirdPartyLibraryMicrosoftCertificate()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("EmptyPKT.dll")
            };

            var strongNameSignInfo = new Dictionary<string, SignInfo>() {};
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>() { };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='Microsoft400'",
            }, 
            expectedWarnings: new[] 
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "EmptyPKT.dll")}' with Microsoft certificate 'Microsoft400'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void ThirdPartyLibraryMicrosoftCertificatePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("EmptyPKT.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[0];
            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
                {
                    "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='Microsoft400'",
                },
                expectedWarnings: new[]
                {
                    $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "EmptyPKT.dll")}' with Microsoft certificate 'Microsoft400'. The library is considered 3rd party library due to its copyright: ''."
                });
        }

        [Fact]
        public void NestedContainer()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("NestedContainer.1.0.0.nupkg")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerTwo.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'",
                "File 'NestedContainer.1.0.0.nupkg' Certificate='NuGet'"
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "lib/native/NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "4", "lib/netcoreapp2.0/ContainerTwo.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "5", "lib/netcoreapp2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "6", "lib/netcoreapp2.1/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "9", "lib/netcoreapp2.0/ContainerOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "ContainerOne.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{GetResourcePath("NestedContainer.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>"
            });
        }

        [Fact]
        public void NestedContainerPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("NestedContainer.1.0.0.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerTwo.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'",
                "File 'NestedContainer.1.0.0.nupkg' Certificate='NuGet'"
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "lib/native/NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "4", "lib/netcoreapp2.0/ContainerTwo.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "5", "lib/netcoreapp2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "6", "lib/netcoreapp2.1/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "9", "lib/netcoreapp2.0/ContainerOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "ContainerOne.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{GetResourcePath("NestedContainer.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>"
            });
        }

        [Fact]
        public void ZipFile()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("test.zip")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'Nested.NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'Nested.SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'test.zip' Certificate=''",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "0", "NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "SOS.NETCore.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "this_is_a_big_folder_name_look/this_is_an_even_more_longer_folder_name/but_this_one_is_ever_longer_than_the_previous_other_two/Nested.NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3", "this_is_a_big_folder_name_look/this_is_an_even_more_longer_folder_name/but_this_one_is_ever_longer_than_the_previous_other_two/Nested.SOS.NETCore.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void ZipFilePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("test.zip"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "ArcadeCertTest" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'Nested.NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'Nested.SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'test.zip' Certificate=''",
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "0", "NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "SOS.NETCore.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "this_is_a_big_folder_name_look/this_is_an_even_more_longer_folder_name/but_this_one_is_ever_longer_than_the_previous_other_two/Nested.NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3", "this_is_a_big_folder_name_look/this_is_an_even_more_longer_folder_name/but_this_one_is_ever_longer_than_the_previous_other_two/Nested.SOS.NETCore.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void MPackFile()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("test.mpack")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'VisualStudio.Mac.Banana.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2'",
                "File 'test.mpack' Certificate=''",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "VisualStudio.Mac.Banana.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void MPackFilePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("test.mpack"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'VisualStudio.Mac.Banana.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName=''",
                "File 'test.mpack' Certificate=''",
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "VisualStudio.Mac.Banana.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName></StrongName>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixAfter()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("test.vsix"),
                GetResourcePath("PackageWithRelationships.vsix"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            },
            new[]
            {
                $"{Path.Combine(_tmpDir, "ContainerSigning", "6", "PackageWithRelationships.vsix")} -> {Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "6", "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixAfterPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("test.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("PackageWithRelationships.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            },
            new[]
            {
                $"{Path.Combine(_tmpDir, "ContainerSigning", "6", "PackageWithRelationships.vsix")} -> {Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "1", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "6", "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixBefore()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("PackageWithRelationships.vsix"),
                GetResourcePath("test.vsix"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixBeforePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("PackageWithRelationships.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("test.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                })
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            // Overriding information
            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixBeforeAndAfter()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("PackageWithRelationships.vsix", relativePath: "A"),
                GetResourcePath("test.vsix"),
                GetResourcePath("PackageWithRelationships.vsix", relativePath: "B"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            },
            new[]
            {
                $"{Path.Combine(_tmpDir, "A", "PackageWithRelationships.vsix")} -> {Path.Combine(_tmpDir, "B", "PackageWithRelationships.vsix")}"
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "A", "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackage_DuplicateVsixBeforeAndAfterPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("PackageWithRelationships.vsix", relativePath: "A"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("test.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("PackageWithRelationships.vsix", relativePath: "B"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'test.vsix' Certificate='VsixSHA2'",
            },
            new[]
            {
                $"{Path.Combine(_tmpDir, "A", "PackageWithRelationships.vsix")} -> {Path.Combine(_tmpDir, "B", "PackageWithRelationships.vsix")}"
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "7", "lib/net461/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8", "lib/netstandard2.0/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "A", "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "test.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>
"
            });
        }

        [Fact]
        public void VsixPackageWithRelationships()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("PackageWithRelationships.vsix")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("3PartySHA2", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'"
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>"
            });
        }

        [Fact]
        public void VsixPackageWithRelationshipsPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("PackageWithRelationships.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "3PartySHA2" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='3PartySHA2' StrongName='ArcadeStrongTest'",
                "File 'PackageWithRelationships.vsix' Certificate='VsixSHA2'"
            });

            ValidateGeneratedProject(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "2", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"">
  <Authenticode>VsixSHA2</Authenticode>
</FilesToSign>"
            });
        }

        [Fact]
        public void CheckFileExtensionSignInfo()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                CreateTestResource("dynalib.dylib"),
                CreateTestResource("javascript.js"),
                CreateTestResource("javatest.jar"),
                CreateTestResource("power.ps1"),
                CreateTestResource("powerc.psc1"),
                CreateTestResource("powerd.psd1"),
                CreateTestResource("powerm.psm1"),
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>();

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'dynalib.dylib' Certificate='DylibCertificate'",
                "File 'javascript.js' Certificate='JSCertificate'",
                "File 'javatest.jar' Certificate='JARCertificate'",
                "File 'power.ps1' Certificate='PSCertificate'",
                "File 'powerc.psc1' Certificate='PSCCertificate'",
                "File 'powerd.psd1' Certificate='PSDCertificate'",
                "File 'powerm.psm1' Certificate='PSMCertificate'",
            });
        }

        [Fact]
        public void CheckFileExtensionSignInfoPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(CreateTestResource("dynalib.dylib"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("javascript.js"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("javatest.jar"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("power.ps1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("powerc.psc1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("powerd.psd1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("powerm.psm1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            ValidateFileSignInfos(
                itemsToSign,
                new ITaskItem[0],
                new ITaskItem[0], 
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
            new[]
            {
                "File 'dynalib.dylib' Certificate='DylibCertificate'",
                "File 'javascript.js' Certificate='JSCertificate'",
                "File 'javatest.jar' Certificate='JARCertificate'",
                "File 'power.ps1' Certificate='PSCertificate'",
                "File 'powerc.psc1' Certificate='PSCCertificate'",
                "File 'powerd.psd1' Certificate='PSDCertificate'",
                "File 'powerm.psm1' Certificate='PSMCertificate'",
            });
        }

        [Fact]
        public void ValidateAppendingCertificate()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("SignedLibrary.dll")
            };

            var dualCertificates = new string[] {
                "DualCertificateName"
            };

            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "31bf3856ad364e35", new SignInfo(dualCertificates.First(), null) }
            };

            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                $"File 'SignedLibrary.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='{dualCertificates.First()}'",
            },
            dualCertificates : dualCertificates);
        }

        [Fact]
        public void ValidateAppendingCertificatePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("SignedLibrary.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var dualCertificates = new ITaskItem[] {
                new TaskItem("DualCertificateName", new Dictionary<string, string>
                {
                    { "DualSigningAllowed", "true" },
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("", new Dictionary<string, string>
                {
                    { "CertificateName", dualCertificates.First().ItemSpec },
                    { "PublicKeyToken", "31bf3856ad364e35" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign,
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild,
                dualCertificates,
                new[]
            {
                $"File 'SignedLibrary.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='{dualCertificates.First()}' StrongName=''",
            });
        }

        [Fact]
        public void PackageWithZipFile()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("PackageWithZip.nupkg")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'test.zip' Certificate=''",
                "File 'PackageWithZip.nupkg' Certificate='NuGet'",
            });
        }

        [Fact]
        public void PackageWithZipFilePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("PackageWithZip.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "ArcadeCertTest" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'test.zip' Certificate=''",
                "File 'PackageWithZip.nupkg' Certificate='NuGet'",
            });
        }

        [Fact]
        public void NestedZipFile()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                GetResourcePath("NestedZip.zip")
            };

            // Default signing information
            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'InnerZipFile.zip' Certificate=''",
                "File 'Mid.SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'MidNativeLibrary.dll' Certificate='Microsoft400'",
                "File 'NestedZip.zip' Certificate=''",
            });
        }

        [Fact]
        public void NestedZipFilePostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("NestedZip.zip"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("ArcadeStrongTest", new Dictionary<string, string>
                {
                    { "CertificateName", "ArcadeCertTest" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[0];

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'NativeLibrary.dll' Certificate='Microsoft400'",
                "File 'SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'InnerZipFile.zip' Certificate=''",
                "File 'Mid.SOS.NETCore.dll' TargetFramework='.NETCoreApp,Version=v1.0' Certificate='Microsoft400'",
                "File 'MidNativeLibrary.dll' Certificate='Microsoft400'",
                "File 'NestedZip.zip' Certificate=''",
            });
        }

        [Fact]
        public void SpecificFileSignInfos()
        {
            // List of files to be considered for signing
            var itemsToSign = new[]
            {
                CreateTestResource("test.js"),
                CreateTestResource("test.jar"),
                CreateTestResource("test.ps1"),
                CreateTestResource("test.psd1"),
                CreateTestResource("test.psm1"),
                CreateTestResource("test.psc1"),
                CreateTestResource("test.dylib"),
                GetResourcePath("EmptyPKT.dll"),
                GetResourcePath("test.vsix"),
                GetResourcePath("Simple.nupkg"),
            };

            var strongNameSignInfo = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "StrongNameValue") },
            };

            // Overriding information
            var fileSignInfo = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("test.js"), "JSCertificate" },
                { new ExplicitCertificateKey("test.jar"), "JARCertificate" },
                { new ExplicitCertificateKey("test.ps1"), "PS1Certificate" },
                { new ExplicitCertificateKey("test.psd1"), "PSD1Certificate" },
                { new ExplicitCertificateKey("test.psm1"), "PSM1Certificate" },
                { new ExplicitCertificateKey("test.psc1"), "PSC1Certificate" },
                { new ExplicitCertificateKey("test.dylib"), "DYLIBCertificate" },
                { new ExplicitCertificateKey("EmptyPKT.dll"), "DLLCertificate" },
                { new ExplicitCertificateKey("test.vsix"), "VSIXCertificate" },
                { new ExplicitCertificateKey("PackageWithRelationships.vsix"), "VSIXCertificate2" },
                { new ExplicitCertificateKey("Simple.dll"), "DLLCertificate2" },
                { new ExplicitCertificateKey("Simple.nupkg"), "NUPKGCertificate" },
                { new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c", ".NETFramework,Version=v4.6.1"), "DLLCertificate3" },
                { new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c", ".NETStandard,Version=v2.0"), "DLLCertificate4" },
                { new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c", ".NETCoreApp,Version=v2.0"), "DLLCertificate5" },
            };

            ValidateFileSignInfos(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
                "File 'test.js' Certificate='JSCertificate'",
                "File 'test.jar' Certificate='JARCertificate'",
                "File 'test.ps1' Certificate='PS1Certificate'",
                "File 'test.psd1' Certificate='PSD1Certificate'",
                "File 'test.psm1' Certificate='PSM1Certificate'",
                "File 'test.psc1' Certificate='PSC1Certificate'",
                "File 'test.dylib' Certificate='DYLIBCertificate'",
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='DLLCertificate'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='DLLCertificate3' StrongName='StrongNameValue'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='DLLCertificate4' StrongName='StrongNameValue'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='DLLCertificate5' StrongName='StrongNameValue'",
                "File 'PackageWithRelationships.vsix' Certificate='VSIXCertificate2'",
                "File 'test.vsix' Certificate='VSIXCertificate'",
                "File 'Simple.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='DLLCertificate2'",
                "File 'Simple.nupkg' Certificate='NUPKGCertificate'",
            }, 
            expectedWarnings: new[]
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "EmptyPKT.dll")}' with Microsoft certificate 'DLLCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "9", "lib/net461/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate3'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "10", "lib/netstandard2.0/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate4'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "16", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate5'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "23", "Simple.dll")}' with Microsoft certificate 'DLLCertificate2'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Fact]
        public void SpecificFileSignInfosPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(CreateTestResource("test.js"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.jar"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.ps1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.psd1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.psm1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.psc1"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(CreateTestResource("test.dylib"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("EmptyPKT.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("test.vsix"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
                new TaskItem(GetResourcePath("Simple.nupkg"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("StrongNameValue", new Dictionary<string, string>
                {
                    { "CertificateName", "ArcadeCertTest" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "BARBuildID", "123" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem("test.js", new Dictionary<string, string>
                {
                    { "CertificateName", "JSCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.jar", new Dictionary<string, string>
                {
                    { "CertificateName", "JARCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.ps1", new Dictionary<string, string>
                {
                    { "CertificateName", "PS1Certificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.psd1", new Dictionary<string, string>
                {
                    { "CertificateName", "PSD1Certificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.psm1", new Dictionary<string, string>
                {
                    { "CertificateName", "PSM1Certificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.psc1", new Dictionary<string, string>
                {
                    { "CertificateName", "PSC1Certificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.dylib", new Dictionary<string, string>
                {
                    { "CertificateName", "DYLIBCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("EmptyPKT.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "DLLCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("test.vsix", new Dictionary<string, string>
                {
                    { "CertificateName", "VSIXCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("PackageWithRelationships.vsix", new Dictionary<string, string>
                {
                    { "CertificateName", "VSIXCertificate2" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("Simple.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "DLLCertificate2" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("Simple.nupkg", new Dictionary<string, string>
                {
                    { "CertificateName", "NUPKGCertificate" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "DLLCertificate3" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "TargetFramework", ".NETFramework,Version=v4.6.1" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "DLLCertificate4" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "TargetFramework", ".NETStandard,Version=v2.0" },
                    { "BARBuildID", "123" }
                }),
                new TaskItem("ProjectOne.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "DLLCertificate5" },
                    { "PublicKeyToken", "581d91ccdfc4ea9c" },
                    { "TargetFramework", ".NETCoreApp,Version=v2.0" },
                    { "BARBuildID", "123" }
                }),
            };

            ValidateFileSignInfos(
                itemsToSign, 
                strongNameSignInfo, 
                fileSignInfo, 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new[]
            {
                "File 'test.js' Certificate='JSCertificate'",
                "File 'test.jar' Certificate='JARCertificate'",
                "File 'test.ps1' Certificate='PS1Certificate'",
                "File 'test.psd1' Certificate='PSD1Certificate'",
                "File 'test.psm1' Certificate='PSM1Certificate'",
                "File 'test.psc1' Certificate='PSC1Certificate'",
                "File 'test.dylib' Certificate='DYLIBCertificate'",
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='DLLCertificate'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='DLLCertificate3' StrongName='StrongNameValue'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='DLLCertificate4' StrongName='StrongNameValue'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='DLLCertificate5' StrongName='StrongNameValue'",
                "File 'PackageWithRelationships.vsix' Certificate='VSIXCertificate2'",
                "File 'test.vsix' Certificate='VSIXCertificate'",
                "File 'Simple.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='DLLCertificate2'",
                "File 'Simple.nupkg' Certificate='NUPKGCertificate'",
            },
            expectedWarnings: new[]
            {
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "EmptyPKT.dll")}' with Microsoft certificate 'DLLCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "9", "lib/net461/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate3'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "10", "lib/netstandard2.0/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate4'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "16", "Contents/Common7/IDE/PrivateAssemblies/ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate5'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "23", "Simple.dll")}' with Microsoft certificate 'DLLCertificate2'. The library is considered 3rd party library due to its copyright: ''."
            });
        }

        [Theory]
        [MemberData(nameof(GetSignableExtensions))]
        public void MissingCertificateName(string extension)
        {
            var needContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".dll", "EmptyPKT.dll" },
                { ".vsix", "Simple.vsix" },
                { ".nupkg", "Simple.nupkg" },
                { ".exe", "Simple.exe" }
            };

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };

            var inputFilePath = needContent.TryGetValue(extension, out var resourcePath) ?
                GetResourcePath(resourcePath) :
                CreateTestResource("test" + extension);

            new Configuration(_tmpDir,
                new string[] { inputFilePath },
                new Dictionary<string, SignInfo>(),
                new Dictionary<ExplicitCertificateKey, string>(),
                new Dictionary<string, SignInfo>(),
                new string[0], task.Log)
                .GenerateListOfFiles();

            Assert.True(task.Log.HasLoggedErrors);
        }

        [Theory]
        [MemberData(nameof(GetSignableExtensions))]
        public void MissingCertificateNamePostBuild(string extension)
        {
            var needContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".dll", "EmptyPKT.dll" },
                { ".vsix", "Simple.vsix" },
                { ".nupkg", "Simple.nupkg" },
                { ".exe", "Simple.exe" }
            };

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };

            var inputFilePath = needContent.TryGetValue(extension, out var resourcePath) ?
                GetResourcePath(resourcePath) :
                CreateTestResource("test" + extension);

            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(inputFilePath, new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            new Configuration(
                _tmpDir,
                itemsToSign,
                new ITaskItem[0],
                new ITaskItem[0],
                new ITaskItem[0],
                new ITaskItem[0],
                task.Log).GenerateListOfFiles();

            Assert.True(task.Log.HasLoggedErrors);
        }

        [Theory]
        [MemberData(nameof(GetSignableExtensions))]
        public void MissingCertificateNameButExtensionIsIgnored(string extension)
        {
            var needContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ".dll", "EmptyPKT.dll" },
                { ".vsix", "Simple.vsix" },
                { ".nupkg", "Simple.nupkg" },
                { ".exe", "Simple.exe" }
            };

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };

            var inputFilePath = needContent.TryGetValue(extension, out var resourcePath) ?
                GetResourcePath(resourcePath) :
                CreateTestResource("test" + extension);

            new Configuration(_tmpDir,
                new string[] { inputFilePath },
                new Dictionary<string, SignInfo>(),
                new Dictionary<ExplicitCertificateKey, string>(),
                new Dictionary<string, SignInfo>() { { extension, SignInfo.Ignore } },
                new string[0], 
                task.Log)
                .GenerateListOfFiles();

            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void CrossGeneratedLibraryWithoutPKT()
        {
            var itemsToSign = new[]
            {
                GetResourcePath("SPCNoPKT.dll")
            };

            ValidateFileSignInfos(itemsToSign, new Dictionary<string, SignInfo>(), new Dictionary<ExplicitCertificateKey, string>(), s_fileExtensionSignInfo, new string[0]);

            ValidateGeneratedProject(itemsToSign, new Dictionary<string, SignInfo>(), new Dictionary<ExplicitCertificateKey, string>(), s_fileExtensionSignInfo, new string[0]);
        }

        [Fact]
        public void CrossGeneratedLibraryWithoutPKTPostBuild()
        {
            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(GetResourcePath("SPCNoPKT.dll"), new Dictionary<string, string>
                {
                    { "BARBuildID", "123" }
                }),
            };

            ValidateFileSignInfos(
                itemsToSign,
                new ITaskItem[0],
                new ITaskItem[0], 
                s_fileExtensionSignInfoPostBuild, 
                new ITaskItem[0],
                new string[0]);

            ValidateGeneratedProject(
                itemsToSign,
                new ITaskItem[0],
                new ITaskItem[0], 
                s_fileExtensionSignInfoPostBuild,
                new ITaskItem[0],
                new string[0]);
        }
    }
}

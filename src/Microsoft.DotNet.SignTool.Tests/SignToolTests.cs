// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignToolTests : IDisposable
    {
        private readonly string _tmpDir;

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

        public SignToolTests()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tmpDir);
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
            var util = new BatchSignUtil(task.BuildEngine, task.Log, signTool, signingInput);

            util.Go();

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
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "CC1D99EE8C2F627E77D019E94B06EBB6D87A4D19E65DDAEF62B6137E49167BAF", "ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}' with Microsoft certificate 'OverriddenCertificate'. The library is considered 3rd party library due to its copyright: ''."
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
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "9F8CCEE4CECF286C80916F13EAB8DF1FC6C9BED5F81E3AFF26747C008D265E5C", "ContainerOne.dll")}' with Microsoft certificate 'ArcadeCertTest'. The library is considered 3rd party library due to its copyright: ''."
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
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3D4466713FF60CA2747166CD22B097B67DAFC7F3487B7F7725945502D66D0B65", "NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "9F8CCEE4CECF286C80916F13EAB8DF1FC6C9BED5F81E3AFF26747C008D265E5C", "ContainerTwo.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "CC1D99EE8C2F627E77D019E94B06EBB6D87A4D19E65DDAEF62B6137E49167BAF", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "9F8CCEE4CECF286C80916F13EAB8DF1FC6C9BED5F81E3AFF26747C008D265E5C", "ContainerOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "19C85C55CB56D9A2533A53A9654D4FDF4B4AEF60A7760DB872CE895EB9B48825", "ContainerOne.1.0.0.nupkg")}"">
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
                "File 'test.zip' Certificate=''",
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3D4466713FF60CA2747166CD22B097B67DAFC7F3487B7F7725945502D66D0B65", "NativeLibrary.dll")}"">
  <Authenticode>Microsoft400</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "FD4596180FC1AB63B2D6A9C6E4086CC15891E41E34F835B593C3879CECAA86B6", "SOS.NETCore.dll")}"">
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
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "VisualStudio.Mac.Banana.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
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
                $"{Path.Combine(_tmpDir, "ContainerSigning", "8CE04F803804FF47F7F96E6D993262E295456CE7A11D65E81530DC030C8BB03C", "PackageWithRelationships.vsix")} -> {Path.Combine(_tmpDir, "PackageWithRelationships.vsix")}"
            });

            ValidateGeneratedProject(itemsToSign, strongNameSignInfo, fileSignInfo, s_fileExtensionSignInfo, new[]
            {
$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8CE04F803804FF47F7F96E6D993262E295456CE7A11D65E81530DC030C8BB03C", "PackageWithRelationships.vsix")}"">
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
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}"">
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
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}"">
  <Authenticode>3PartySHA2</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}"">
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
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
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
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate3'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate4'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}' with Microsoft certificate 'DLLCertificate5'. The library is considered 3rd party library due to its copyright: ''.",
                $@"SIGN001: Signing 3rd party library '{Path.Combine(_tmpDir, "ContainerSigning", "DCD5A867480FEE469A37FF890446F4A34B7770EDDE4DD939A0313F1D3B3AAF99", "Simple.dll")}' with Microsoft certificate 'DLLCertificate2'. The library is considered 3rd party library due to its copyright: ''."
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
    }
}

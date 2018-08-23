// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.DotNet.SignTool.Tests
{
    public class SignToolTests : IDisposable
    {
        private readonly bool _isWindows;
        private readonly string _tmpDir;

        public SignToolTests()
        {
            // As of now we don't have "mscoree.dll" on Linux. This DLL is used when checking
            // if the file is strong name signed: SignTool/ContentUtil.NativeMethods
            // Therefore, test cases won't execute in fully on non-Windows machines.
            _isWindows = Environment.OSVersion.VersionString.Contains("Windows");

            _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tmpDir);            
        }

        private string GetResourcePath(string name)
        {
            var srcPath = Path.Combine(Path.GetDirectoryName(typeof(SignToolTests).Assembly.Location), "Resources", name);
            var dstPath = Path.Combine(_tmpDir, name);

            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath);
            }

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
            Dictionary<ExplicitCertificateKey, string> signingOverridingInfos, 
            string[] expectedXmlElementsPerSingingRound)
        {
            if (!_isWindows) return;

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };

            // The path to MSBuild will always be null in these tests, this will force
            // the signing logic to call our FakeBuildEngine.BuildProjectFile with a path
            // to the XML that store the content of the would be Microbuild sign request.
            var signToolArgs = new SignToolArgs(_tmpDir, microBuildCorePath: "MicroBuildCorePath", testSign: true, msBuildPath: null, _tmpDir);

            var signTool = new ValidationOnlySignTool(signToolArgs);
            var signingInput = new Configuration(signToolArgs.TempDir, itemsToSign, strongNameSignInfo, signingOverridingInfos, publishUri: null, task.Log).GenerateListOfFiles();
            var util = new BatchSignUtil(task.BuildEngine, task.Log, signTool, signingInput, null);

            util.Go();

            // The list of files that would be signed was captured inside the FakeBuildEngine,
            // here we check if that matches what we expected
            var fakeEngine = (FakeBuildEngine)task.BuildEngine;

            AssertEx.Equal(
                expectedXmlElementsPerSingingRound, 
                fakeEngine.FilesToSign.Select(round => string.Join(Environment.NewLine, round)));

            Assert.False(task.Log.HasLoggedErrors);
        }

        private void ValidateFileSignInfos(
            string[] itemsToSign,
            Dictionary<string, SignInfo> strongNameSignInfo,
            Dictionary<ExplicitCertificateKey, string> signingOverridingInfos,
            string[] expected)
        {
            if (!_isWindows) return;

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };
            var signingInput = new Configuration(_tmpDir, itemsToSign, strongNameSignInfo, signingOverridingInfos, publishUri: null, task.Log).GenerateListOfFiles();

            AssertEx.Equal(expected, signingInput.FilesToSign.Select(f => f.ToString()));
            Assert.False(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void EmptySigningList()
        {
            var ExplicitSignItems = new string[0];

            var StrongNameSignInfo = new Dictionary<string, SignInfo>();

            var FileSignInfo = new Dictionary<ExplicitCertificateKey, string>();

            var task = new SignToolTask { BuildEngine = new FakeBuildEngine() };
            var signingInput = new Configuration(_tmpDir, ExplicitSignItems, StrongNameSignInfo, FileSignInfo, publishUri: null, task.Log).GenerateListOfFiles();

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
            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "", new SignInfo("ManagedNoStrongNameCert") },
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[]
            {
                "File 'NativeLibrary.dll' Certificate='MicrosoftSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
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
            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("ProjectOne.dll", "581d91ccdfc4ea9c"), "OverriddenCertificate" }
            };

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[] 
            {
                "File 'NativeLibrary.dll' Certificate='MicrosoftSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='OverriddenCertificate' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
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
            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("NativeLibrary.dll"), "OverriddenCertificate1" },
                { new ExplicitCertificateKey("ProjectOne.dll"), "OverriddenCertificate2" }
            };

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[]
            {
                "File 'NativeLibrary.dll' Certificate='OverriddenCertificate1'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='OverriddenCertificate2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='OverriddenCertificate2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='OverriddenCertificate2' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='OverriddenCertificate2' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'"
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
            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("EmptyPKT.dll"), "OverriddenCertificate" }
            };

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[] 
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='OverriddenCertificate'",
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

            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "", new SignInfo("DefaultCertificate") }
            };

            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>() { };

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[]
            {
                "File 'EmptyPKT.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='DefaultCertificate'",
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

            var signingInformation = new Dictionary<string, SignInfo>()
            {
                {  "", new SignInfo("DefaultCertificate") }
            };

            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>()
            {
                { new ExplicitCertificateKey("CustomTargetFrameworkAttribute.dll", targetFramework: ".NETFramework,Version=v2.0"), "OverriddenCertificate" }
            };

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[] 
            {
                "File 'CustomTargetFrameworkAttribute.dll' TargetFramework='.NETFramework,Version=v2.0' Certificate='OverriddenCertificate'",
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
            var signingInformation = new Dictionary<string, SignInfo>()
            {
                { "581d91ccdfc4ea9c", new SignInfo("ArcadeCertTest", "ArcadeStrongTest") }
            };

            // Overriding information
            var signingOverridingInformation = new Dictionary<ExplicitCertificateKey, string>();

            ValidateFileSignInfos(itemsToSign, signingInformation, signingOverridingInformation, new[] 
            {
                "File 'NativeLibrary.dll' Certificate='MicrosoftSHA2'",
                "File 'ProjectOne.dll' TargetFramework='.NETFramework,Version=v4.6.1' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETCoreApp,Version=v2.1' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ProjectOne.dll' TargetFramework='.NETStandard,Version=v2.0' Certificate='ArcadeCertTest' StrongName='ArcadeStrongTest'",
                "File 'ContainerOne.1.0.0.nupkg' Certificate='NuGet'",
                "File 'NestedContainer.1.0.0.nupkg' Certificate='NuGet'"
            });

            ValidateGeneratedProject(itemsToSign, signingInformation, signingOverridingInformation, new[]
            {
$@"<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "3D4466713FF60CA2747166CD22B097B67DAFC7F3487B7F7725945502D66D0B65", "NativeLibrary.dll")}"">
  <Authenticode>MicrosoftSHA2</Authenticode>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "B306A318B3A11BF342995F6A1FC5AADF5DB4DD49F4EFF7E013D31208DD58EBDC", "ProjectOne.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "9F8CCEE4CECF286C80916F13EAB8DF1FC6C9BED5F81E3AFF26747C008D265E5C", "ContainerOne.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "8492D8CE69F362AAB589989D6B9687C53B732E73493492D06A5650A86B6D4D20", "ProjectOne.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "CC1D99EE8C2F627E77D019E94B06EBB6D87A4D19E65DDAEF62B6137E49167BAF", "ProjectOne.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>
<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "47F202CA51AD708535A01E96B95027042F8448333D86FA7D5F8D66B67644ACEC", "ProjectOne.dll")}"">
  <Authenticode>ArcadeCertTest</Authenticode>
  <StrongName>ArcadeStrongTest</StrongName>
</FilesToSign>",

$@"<FilesToSign Include=""{Path.Combine(_tmpDir, "ContainerSigning", "19C85C55CB56D9A2533A53A9654D4FDF4B4AEF60A7760DB872CE895EB9B48825", "ContainerOne.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>",

$@"<FilesToSign Include=""{GetResourcePath("NestedContainer.1.0.0.nupkg")}"">
  <Authenticode>NuGet</Authenticode>
</FilesToSign>"
            });
        }
    }
}

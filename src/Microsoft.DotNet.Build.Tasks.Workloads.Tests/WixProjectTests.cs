// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class WixProjectTests : TestBase
    {
        [WindowsOnlyFact]
        public void ItGeneratesAnSdkStyleProject()
        {
            var wixproj = new WixProject("5.0.2");
            string projectDir = GetTestCaseDirectory();
            string wixProjPath = Path.Combine(projectDir, "msi.wixproj");
            Directory.CreateDirectory(projectDir);

            wixproj.Save(wixProjPath);

            string projectContents = File.ReadAllText(wixProjPath);

            Assert.StartsWith(@"<Project Sdk=""Microsoft.WixToolset.Sdk/5.0.2""", projectContents);            
        }

        [WindowsOnlyTheory]
        [InlineData("Microsoft.WixToolset.Heat", null, false, @"<PackageReference Include=""Microsoft.WixToolset.Heat"" />")]
        [InlineData("Microsoft.WixToolset.Heat", "5.0.3", false, @"<PackageReference Include=""Microsoft.WixToolset.Heat"" Version=""5.0.3"" />")]
        [InlineData("Microsoft.WixToolset.Heat", "5.0.3", true, @"<PackageReference Include=""Microsoft.WixToolset.Heat"" VersionOverride=""5.0.3"" />")]
        public void PackageReferencesCanBeAdded(string packageId, string packageVersion, bool overridePackageVersions,
            string expectedPackageReference)
        {
            var wixproj = new WixProject("5.0.2") 
            { 
                OverridePackageVersions = overridePackageVersions 
            };

            string projectDir = GetTestCaseDirectory();
            string wixProjPath = Path.Combine(projectDir, "msi.wixproj");
            Directory.CreateDirectory(projectDir);

            wixproj.AddPackageReference(packageId, packageVersion);                
            wixproj.Save(wixProjPath);

            string projectContents = File.ReadAllText(wixProjPath);

            Assert.Contains("Microsoft.WixToolset.Sdk/5.0.2", projectContents);
            Assert.Contains(expectedPackageReference, projectContents);
        }

        [WindowsOnlyFact]
        public void PreprocessorDefinitionsCanBeAdded()
        {
            var wixproj = new WixProject("5.0.2");
            string projectDir = GetTestCaseDirectory();
            string wixProjPath = Path.Combine(projectDir, "msi.wixproj");
            Directory.CreateDirectory(projectDir);

            wixproj.AddPreprocessorDefinition("Foo", "  Bar  ");
            wixproj.Save(wixProjPath);

            string projectContents = File.ReadAllText(wixProjPath);

            Assert.Contains("<DefineConstants>$(DefineConstants);Foo=  Bar  </DefineConstants", projectContents);
        }

        [WindowsOnlyFact]
        public void PropertiesCanBeAdded()
        {
            var wixproj = new WixProject("5.0.2");
            string projectDir = GetTestCaseDirectory();
            string wixProjPath = Path.Combine(projectDir, "msi.wixproj");
            Directory.CreateDirectory(projectDir);

            wixproj.AddProperty("InstallerPlatform", "x64");
            wixproj.Save(wixProjPath);
            
            string projectContents = File.ReadAllText(wixProjPath);

            Assert.Contains("<InstallerPlatform>x64</InstallerPlatform", projectContents);
        }

        [WindowsOnlyFact]
        public void HarvestDirectoriesCanBeAdded()
        {
            var wixproj = new WixProject("5.0.2");
            string projectDir = GetTestCaseDirectory();
            string wixProjPath = Path.Combine(projectDir, "msi.wixproj");
            Directory.CreateDirectory(projectDir);

            wixproj.AddHarvestDirectory(@"x\y\z", "SOMEDIR", "MyVar", "CG_Test");
            // This will select the default component group.
            wixproj.AddHarvestDirectory(@"a\b\c", "SOMEDIR2", "MyVar2");
            // Omit the preprocessor variable override.
            wixproj.AddHarvestDirectory(@"aa\bb\cc", "SOMEDIR3");
            // Omit the preprocessor variable override.
            wixproj.AddHarvestDirectory(@"xx\yy\zz");
            wixproj.Save(wixProjPath);

            string projectContents = File.ReadAllText(wixProjPath);
            Assert.Contains(@"<HarvestDirectory Include=""x\y\z"" ComponentGroupName=""CG_Test"" DirectoryRefId=""SOMEDIR"" PreprocessorVariable=""MyVar"" SuppressRegistry=""true"" SuppressRootDirectory=""true"" />", projectContents,StringComparison.OrdinalIgnoreCase);
            Assert.Contains(@"<HarvestDirectory Include=""a\b\c"" ComponentGroupName=""CG_PackageContents"" DirectoryRefId=""SOMEDIR2"" PreprocessorVariable=""MyVar2"" SuppressRegistry=""true"" SuppressRootDirectory=""true"" />", projectContents, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(@"<HarvestDirectory Include=""aa\bb\cc"" ComponentGroupName=""CG_PackageContents"" DirectoryRefId=""SOMEDIR3"" SuppressRegistry=""true"" SuppressRootDirectory=""true"" />", projectContents, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(@"<HarvestDirectory Include=""xx\yy\zz"" ComponentGroupName=""CG_PackageContents"" SuppressRegistry=""true"" SuppressRootDirectory=""true"" />", projectContents, StringComparison.OrdinalIgnoreCase);
        }
    }
}

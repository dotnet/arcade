// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.NuGet.Tests
{
    public class PackNuSpecTests : IDisposable
    {
        private readonly string _tempDir;

        public PackNuSpecTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void CreatesPackage()
        {
            var outputPath = Path.Combine(_tempDir, $"TestPackage.2.0.0.nupkg");
            Directory.CreateDirectory(Path.Combine(_tempDir, "tools"));
            Directory.CreateDirectory(Path.Combine(_tempDir, "lib", "netstandard2.0"));
            File.WriteAllText(Path.Combine(_tempDir, "tools", "test.sh"), "");
            File.WriteAllText(Path.Combine(_tempDir, "lib", "netstandard2.0", "TestPackage.dll"), "");

            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>TestPackage</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                  <files>
                    <file src=`tools\*` target=`tools/` />
                    <file src=`lib/netstandard2.0/TestPackage.dll` target=`lib/netstandard2.0/` />
                  </files>
                </package>
                ");

            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = new MockEngine(),
                OutputDirectory = _tempDir,
                Version = "2.0.0+sha-1234",
            };

            Assert.True(task.Execute(), "The task should have passed");
            Assert.True(File.Exists(outputPath), "Should have produced a nupkg file in " + _tempDir);
            var result = Assert.Single(task.Packages);
            Assert.Equal(outputPath, result.ItemSpec);
            using (var reader = new PackageArchiveReader(outputPath))
            {
                var libItems = reader.GetLibItems().ToList();
                var libItem = Assert.Single(libItems);
                Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard20, libItem.TargetFramework);
                var assembly = Assert.Single(libItem.Items);
                Assert.Equal("lib/netstandard2.0/TestPackage.dll", assembly);
                Assert.Equal(new NuGetVersion("2.0.0+sha-1234"), reader.GetIdentity().Version);
                Assert.Contains(reader.GetFiles(), f => f == "tools/test.sh");
            }
        }

        [Fact]
        public void AppliesProperties()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasProperties</id>
                    <version>$version$</version>
                    <authors>Microsoft</authors>
                    <description>$description$</description>
                    <copyright>$copyright$</copyright>
                    <dependencies>
                      <dependency id=`somepackage` version=`1.0.0` />
                    </dependencies>
                  </metadata>
                  <files />
                </package>
                ");

            var version = "1.2.3";
            var description = "A test package\n\n\nwith newlines";
            var outputPath = Path.Combine(_tempDir, $"HasProperties.{version}.nupkg");
            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = new MockEngine(),
                OutputDirectory = _tempDir,
                Properties = new[] { $"version={version}", "", "", $" description ={description}", "copyright=", },
            };

            Assert.True(task.Execute(), "The task should have passed");
            Assert.True(File.Exists(outputPath), "Should have produced a nupkg file in " + _tempDir);

            using (var reader = new PackageArchiveReader(outputPath))
            {
                var metadata = new PackageBuilder(reader.GetNuspec(), basePath: null);
                Assert.Equal(version, metadata.Version.ToString());
                Assert.Empty(metadata.Copyright);
                Assert.Equal(description, metadata.Description);
            }
        }

        [Fact]
        public void AddsDependencies()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasDependencies</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                    <dependencies>
                        <dependency id=`AlreadyInNuspec` version=`[2.0.0]` />
                    </dependencies>
                  </metadata>
                  <files />
                </package>
                ");

            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = new MockEngine(),
                OutputDirectory = _tempDir,
                Dependencies = new[]
                {
                    new TaskItem("OtherPackage", new Hashtable { ["Version"] = "[1.0.0, 2.0.0)"}),
                    new TaskItem("PackageInTfm", new Hashtable { ["TargetFramework"] = "netstandard1.0", ["Version"] = "0.1.0-beta" }),
                    new TaskItem("PackageInTfm", new Hashtable { ["TargetFramework"] = "netstandard1.1", ["Version"] = "0.2.0-beta" }),
                }
            };

            Assert.True(task.Execute(), "The task should have passed");
            var result = Assert.Single(task.Packages);

            using (var reader = new PackageArchiveReader(result.ItemSpec))
            {
                var metadata = new PackageBuilder(reader.GetNuspec(), basePath: null);

                var noTfmGroup = Assert.Single(metadata.DependencyGroups, d => d.TargetFramework.Equals(NuGetFramework.UnsupportedFramework));
                Assert.Equal(2, noTfmGroup.Packages.Count());
                Assert.Single(noTfmGroup.Packages, p => p.Id == "OtherPackage" && p.VersionRange.Equals(VersionRange.Parse("[1.0.0, 2.0.0)")));
                Assert.Single(noTfmGroup.Packages, p => p.Id == "AlreadyInNuspec" && p.VersionRange.Equals(VersionRange.Parse("[2.0.0]")));

                var netstandard10Group = Assert.Single(metadata.DependencyGroups, d => d.TargetFramework.Equals(FrameworkConstants.CommonFrameworks.NetStandard10));
                var package1 = Assert.Single(netstandard10Group.Packages);
                Assert.Equal("PackageInTfm", package1.Id);
                Assert.Equal(VersionRange.Parse("0.1.0-beta"), package1.VersionRange);

                var netstandard11Group = Assert.Single(metadata.DependencyGroups, d => d.TargetFramework.Equals(FrameworkConstants.CommonFrameworks.NetStandard11));
                var package2 = Assert.Single(netstandard11Group.Packages);
                Assert.Equal("PackageInTfm", package2.Id);
                Assert.Equal(VersionRange.Parse("0.2.0-beta"), package2.VersionRange);
            }
        }

        [Fact]
        public void WarnIfMissingFilesNodes()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasNoFiles</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                </package>
                ");

            var engine = new MockEngine();
            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = engine,
                OutputDirectory = _tempDir,
            };
            Assert.True(task.Execute());
            var warning = Assert.Single(engine.Warnings);
            Assert.Equal(NugetErrors.NuspecMissingFilesNode, warning.Code);
        }

        [Fact]
        public void PacksFiles()
        {
            var files = new[]
            {
                Path.Combine("lib", "netstandard1.0", "_._"),
                "top.txt",
            };

            var items = new List<ITaskItem>();

            foreach (var file in files)
            {
                var path = Path.Combine(_tempDir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "");
                items.Add(new TaskItem(path, new Hashtable { ["PackagePath"] = file }));
            }

            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasFiles</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                  <files />
                </package>
                ");

            var engine = new MockEngine();
            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = engine,
                PackageFiles = items.ToArray(),
                OutputDirectory = _tempDir,
            };

            Assert.True(task.Execute());
            var result = Assert.Single(task.Packages);

            using (var reader = new PackageArchiveReader(result.ItemSpec))
            {
                Assert.Contains("lib/netstandard1.0/_._", reader.GetFiles());
                Assert.Contains("top.txt", reader.GetFiles());
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("somedir/")]
        public void FailsForBadPackagePath(string path)
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasFiles</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                  <files />
                </package>
                ");

            var engine = new MockEngine { ContinueOnError = true };
            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = engine,
                PackageFiles = new[] { new TaskItem("file.txt", new Hashtable { ["PackagePath"] = path }) },
                OutputDirectory = _tempDir,
            };

            Assert.False(task.Execute(), "Task should fail");
            var error = Assert.Single(engine.Errors);
            Assert.Equal(NugetErrors.InvalidPackagePathMetadata, error.Code);
        }

        [Fact]
        public void SetsLibraryIncludeFlagsOnDependency()
        {
            var nuspec = CreateNuspec(@"
                <?xml version=`1.0` encoding=`utf-8`?>
                <package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
                  <metadata>
                    <id>HasDependencies</id>
                    <version>1.0.0</version>
                    <authors>Test</authors>
                    <description>Test</description>
                  </metadata>
                  <files />
                </package>
                ");

            var task = new PackNuSpec
            {
                FilePath = nuspec,
                BaseDirectory = _tempDir,
                BuildEngine = new MockEngine(),
                OutputDirectory = _tempDir,
                Dependencies = new[]
                {
                    new TaskItem("Include", new Hashtable { ["Version"] = "1.0.0", ["IncludeAssets"] = "Build;Analyzers"}),
                    new TaskItem("Exclude", new Hashtable { ["Version"] = "1.0.0", ["ExcludeAssets"] = "Compile;Native"}),
                    new TaskItem("Both", new Hashtable { ["Version"] = "1.0.0", ["IncludeAssets"] = "Build; Analyzers", ["ExcludeAssets"] = "Build; Native; ContentFiles"}),
                }
            };

            Assert.True(task.Execute(), "The task should have passed");
            var result = Assert.Single(task.Packages);

            using (var reader = new PackageArchiveReader(result.ItemSpec))
            {
                var metadata = new PackageBuilder(reader.GetNuspec(), basePath: null);
                var packages = Assert.Single(metadata.DependencyGroups).Packages;
                Assert.Equal(3, packages.Count());

                var include = Assert.Single(packages, p => p.Id == "Include").Include;
                Assert.Equal(new[] { "Build", "Analyzers" }, include);

                var exclude = Assert.Single(packages, p => p.Id == "Exclude").Exclude;
                Assert.Equal(new[] { "Compile", "Native" }, exclude);

                var both = Assert.Single(packages, p => p.Id == "Both");
                Assert.Equal(new[] { "Build", "Analyzers" }, both.Include);
                Assert.Equal(new[] { "Build", "Native", "ContentFiles" }, both.Exclude);
            }
        }

        [Fact]
        public void FailsIfBothOutputPathAndDestinationFolderAreGiven()
        {
            var engine = new MockEngine { ContinueOnError = true };
            var task = new PackNuSpec
            {
                BuildEngine = engine,
                OutputPath = _tempDir,
                OutputDirectory = _tempDir,
                FilePath = CreateNuspec(""),
            };

            Assert.False(task.Execute(), "Task should fail");
            Assert.Contains("Either DestinationFolder and OutputPath must be specified, but only one, not both.", engine.Errors.Select(e => e.Message));
        }

        [Fact]
        public void FailsIfNeitherOutputPathAndDestinationFolderAreGiven()
        {
            var engine = new MockEngine { ContinueOnError = true };
            var task = new PackNuSpec
            {
                BuildEngine = engine,
                FilePath = CreateNuspec(""),
            };

            Assert.False(task.Execute(), "Task should fail");
            Assert.Contains("Either DestinationFolder and OutputPath must be specified, but only one, not both.", engine.Errors.Select(e => e.Message));
        }

        private string CreateNuspec(string xml)
        {
            var nuspecPath = Path.Combine(_tempDir, Path.GetRandomFileName() + ".nuspec");
            File.WriteAllText(nuspecPath, xml.Replace('`', '"').TrimStart());
            return nuspecPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                Console.WriteLine("Failed to delete " + _tempDir);
            }
        }
    }
}

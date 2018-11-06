using Microsoft.DotNet.DarcLib;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.Darc.Tests
{
    public class DependencyAddUpdateTests
    {
        /// <summary>
        ///     Verifies that empty updates+rewrite don't do odd things.
        ///     Should format the xml to canonical form though.
        /// </summary>
        [Fact]
        public void EmptyVersions1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(EmptyVersions1), async driver =>
            {
                await driver.UpdateDependenciesAsync(new List<DependencyDetail>());
            });
        }

        /// <summary>
        ///     Verifies that non-empty versions don't get reformatted in odd ways.
        /// </summary>
        [Fact]
        public void EmptyVersions2()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(EmptyVersions2), async driver =>
            {
                await driver.UpdateDependenciesAsync(new List<DependencyDetail>());
            });
        }

        /// <summary>
        /// Add a basic dependency.  Versions.props has a default xmlns on the Project element.
        /// </summary>
        [Fact]
        public void AddProductDependency1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddProductDependency1), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.3"
                    },
                    DependencyType.Product);
            });
        }

        /// <summary>
        /// Add a basic dependency.  Versions.props
        /// </summary>
        [Fact]
        public void AddProductDependency2()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddProductDependency2), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.3"
                    },
                    DependencyType.Product);
            });
        }

        /// <summary>
        /// Add a dependency and then add it again.  Should throw on second add.
        /// 
        /// </summary>
        [Fact]
        public void AddProductDependency3()
        {
            // Use assets from #2.
            DependencyTestDriver.TestAndCompareOutput(nameof(AddProductDependency2), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.3"
                    },
                    DependencyType.Product);

                await Assert.ThrowsAsync<DependencyException>(
                    async () => await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "67890",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.4"
                    },
                    DependencyType.Product));
            });
        }

        [Fact]
        public void AddProductDependency4()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddProductDependency4), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.3"
                    },
                    DependencyType.Product);

                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "67890",
                        Name = "Foo.Bar2",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.4"
                    },
                    DependencyType.Product);

                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "67890",
                        Name = "Foo.Bar3",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "1.2.4"
                    },
                    DependencyType.Toolset);
            });
        }

        /// <summary>
        /// Add, where the package version isn't in the details file, but is in Versions.props.
        /// This this case, should update Versions.props
        /// </summary>
        [Fact(Skip = "Not able to update existing version info when adding new dependency. https://github.com/dotnet/arcade/issues/1095")]
        public void AddProductDependency5()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddProductDependency5), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "123",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/bop/bop",
                        Version = "1.2.3"
                    },
                    DependencyType.Product);
            });
        }

        /// <summary>
        /// Update a dependency only existing in Versions.Details.xml
        /// </summary>
        [Fact]
        public void UpdateDependencies1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(UpdateDependencies1), async driver =>
            {
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "4",
                            Name = "Existing.Dependency",
                            RepoUri = "https://foo.com/foo/bar",
                            Version = "4.5.6"
                        }
                    });
            });
        }

        /// <summary>
        /// Attempt to update a non-existing dependency
        /// </summary>
        [Fact]
        public void UpdateDependencies2()
        {
            // Use inputs from previous test.
            DependencyTestDriver.TestNoCompare(nameof(UpdateDependencies1), async driver =>
            {
                await Assert.ThrowsAsync<DependencyException>(async () => await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "4",
                            Name = "Foo.Bar",
                            RepoUri = "https://foo.com/foo/bar",
                            Version = "4.5.6"
                        }
                    }));
            });
        }

        /// <summary>
        /// Update a dependency with new casing.
        /// </summary>
        [Fact]
        public void UpdateDependencies3()
        {
            // Use inputs from previous test.
            DependencyTestDriver.TestAndCompareOutput(nameof(UpdateDependencies3), async driver =>
            {
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "4",
                            Name = "Existing.DEPendency",
                            RepoUri = "https://foo.com/foo/bar",
                            Version = "4.5.6"
                        }
                    });
            });
        }

        /// <summary>
        /// Update a dependency with new casing and alternate property names
        /// </summary>
        [Fact]
        public void UpdateDependencies4()
        {
            // Use inputs from previous test.
            DependencyTestDriver.TestAndCompareOutput(nameof(UpdateDependencies4), async driver =>
            {
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "4",
                            Name = "Existing.DEPendency",
                            RepoUri = "https://foo.com/foo/bar",
                            Version = "4.5.6"
                        }
                    });
            });
        }

        /// <summary>
        /// Support both Version and PackageVersion properties in Versions.props.
        /// When adding, use what's already in the file.
        /// </summary>
        [Fact]
        public void SupportAlternateVersionPropertyFormats1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(SupportAlternateVersionPropertyFormats1), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "4.5.6"
                    },
                    DependencyType.Product);
            });
        }

        /// <summary>
        /// Support both Version and PackageVersion properties in Versions.props.
        /// </summary>
        [Fact]
        public void SupportAlternateVersionPropertyFormats2()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(SupportAlternateVersionPropertyFormats2), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "12345",
                        Name = "Foo.Bar",
                        RepoUri = "https://foo.com/foo/bar",
                        Version = "4.5.6"
                    },
                    DependencyType.Product);
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "4",
                            Name = "Existing.Dependency",
                            RepoUri = "https://foo.com/foo/bar",
                            Version = "4.5.6"
                        }
                    });
            });
        }

        /// <summary>
        /// Add an arcade dependency.
        /// - Does not currently test script download
        /// </summary>
        [Fact]
        public void AddArcadeDependency1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddArcadeDependency1), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "123",
                        Name = "Microsoft.DotNet.Arcade.Sdk",
                        RepoUri = "https://github.com/dotnet/arcade",
                        Version = "1.0"
                    },
                    DependencyType.Toolset);
            });
        }

        /// <summary>
        /// Add an arcade dependency.  Not in version.details but in global.json  Should update.
        /// - Does not currently test script download
        /// </summary>
        [Fact(Skip = "Not able to update existing version info when adding new dependency. https://github.com/dotnet/arcade/issues/1095")]
        public void AddArcadeDependency2()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(AddArcadeDependency1), async driver =>
            {
                await driver.AddDependencyAsync(
                    new DependencyDetail
                    {
                        Commit = "123",
                        Name = "Microsoft.DotNet.Arcade.Sdk",
                        RepoUri = "https://github.com/dotnet/arcade",
                        Version = "2.0"
                    },
                    DependencyType.Toolset);
            });
        }

        /// <summary>
        /// Update the arcade dependency to a new version.
        /// - Does not currently test script download
        /// </summary>
        [Fact]
        public void UpdateArcadeDependency1()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(UpdateArcadeDependency1), async driver =>
            {
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "456",
                            Name = "Microsoft.DotNet.Arcade.Sdk",
                            RepoUri = "https://github.com/dotnet/arcade",
                            Version = "2.0"
                        }
                    });
            });
        }

        /// <summary>
        /// Update the arcade dependency to a new version, though it's not in global.json
        /// </summary>
        [Fact]
        public void UpdateArcadeDependency2()
        {
            DependencyTestDriver.TestAndCompareOutput(nameof(UpdateArcadeDependency2), async driver =>
            {
                await driver.UpdateDependenciesAsync(
                    new List<DependencyDetail> {
                        new DependencyDetail
                        {
                            Commit = "456",
                            Name = "Microsoft.DotNet.Arcade.Sdk",
                            RepoUri = "https://github.com/dotnet/arcade",
                            Version = "2.0"
                        }
                    });
            });
        }

        /// <summary>
        ///     Sentinel test for checking that the normal version suffix isn't the end
        ///     of the alternate suffix. While other tests will fail if this is the case,
        ///     this makes diagnosing it easier.
        /// </summary>
        [Fact]
        public void CheckAlternateSuffix()
        {
            Assert.False(VersionFiles.VersionPropsAlternateVersionElementSuffix.EndsWith(
                         VersionFiles.VersionPropsVersionElementSuffix),
                         "The alternate version element suffix should not end with the preferred suffix. " +
                         "Doing so will break the updating logic.");
        }
    }
}

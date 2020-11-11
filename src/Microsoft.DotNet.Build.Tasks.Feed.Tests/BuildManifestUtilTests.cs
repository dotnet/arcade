// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class BuildManifestUtilTests
    {
        #region Standard test values

        private const string _testAzdoRepoUri = "https://dnceng@dev.azure.com/dnceng/internal/_git/dotnet-buildtest";
        private const string _testBuildBranch = "foobranch";
        private const string _testBuildCommit = "664996a16fa9228cfd7a55d767deb31f62a65f51";
        private const string _testAzdoBuildId = "89999999";
        private const string _testInitialLocation = "As they say....Location Location Location!";
        private static readonly string[] _defaultManifestBuildData = new string[]
        {
                $"InitialAssetsLocation={_testInitialLocation}"
        };

        #endregion

        #region Artifact related tests
        /// <summary>
        /// A model with no input artifacts is invalid
        /// </summary>
        [Fact]
        public void AttemptToCreateModelWithNoArtifactsFails()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());

            Assert.Throws<ArgumentNullException>(() =>
                BuildManifestUtil.CreateModelFromItems(null, null,
                null, null, null, null, _testAzdoBuildId, null, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper));
        }

        /// <summary>
        /// Relatively unified test of manifest artifact parsing. Focuses on 3 things:
        /// - That symbol packages are correctly identified as blobs in the correct locations
        /// - Blobs and packages are split into appropriate categories
        /// - Artifact metadata is preserved, which includes the attributes
        /// 
        /// Because there is a ton of overlap between the individual tests of this functionality
        /// (they essentially all need to verify the same permutations), these tests are combined into
        /// one.
        /// </summary>
        [Fact]
        public void ManifestArtifactParsingTest()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));

            const string bopSymbolsNupkg = "foo/bar/baz/bop.symbols.nupkg";
            string bobSymbolsExpectedId = $"assets/symbols/{Path.GetFileName(bopSymbolsNupkg)}";
            const string bopSnupkg = "foo/bar/baz/bop.symbols.nupkg";
            string bopSnupkgExpectedId = $"assets/symbols/{Path.GetFileName(bopSnupkg)}";
            const string zipArtifact = "foo/bar/baz/bing.zip";

            var artifacts = new ITaskItem[]
            {
                new TaskItem(bopSymbolsNupkg, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=true;Category=SMORKELER" },
                    { "ThisIsntArtifactMetadata", "YouGoofed!" }
                }),
                new TaskItem(bopSnupkg, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=false;Category=SNORPKEG;" }
                }),
                // Include a package and a fake zip too
                // Note that the relative blob path is a "first class" attribute,
                // not parsed from ManifestArtifactData
                new TaskItem(zipArtifact, new Dictionary<string, string>
                {
                    { "RelativeBlobPath", zipArtifact },
                    { "ManifestArtifactData", "ARandomBitOfMAD=" },
                }),
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    // This isn't recognized or used for a nupkg
                    { "RelativeBlobPath", zipArtifact },
                    { "ManifestArtifactData", "ShouldWePushDaNorpKeg=YES" },
                })
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.False(taskLoggingHelper.HasLoggedErrors);
            // When Maestro sees a symbol package, it is supposed to re-do the symbol package path to
            // be assets/symbols/<file-name>
            Assert.Collection(model.Artifacts.Blobs,
                blob =>
                {
                    Assert.Equal(bobSymbolsExpectedId, blob.Id);
                    Assert.True(blob.NonShipping);
                    Assert.Collection(blob.Attributes,
                        attr =>
                        {
                            Assert.Equal("NonShipping", attr.Key);
                            Assert.Equal("true", attr.Value);
                        },
                        attr => {
                            Assert.Equal("Category", attr.Key);
                            Assert.Equal("SMORKELER", attr.Value);
                        },
                        attr => {
                            Assert.Equal("Id", attr.Key);
                            Assert.Equal(bobSymbolsExpectedId, attr.Value);
                        });
                },
                blob =>
                {
                    Assert.Equal(bopSnupkgExpectedId, blob.Id);
                    Assert.False(blob.NonShipping);
                    Assert.Collection(blob.Attributes,
                        attr =>
                        {
                            Assert.Equal("NonShipping", attr.Key);
                            Assert.Equal("false", attr.Value);
                        },
                        attr => {
                            Assert.Equal("Category", attr.Key);
                            Assert.Equal("SNORPKEG", attr.Value);
                        },
                        attr => {
                            Assert.Equal("Id", attr.Key);
                            Assert.Equal(bopSnupkgExpectedId, attr.Value);
                        });
                },
                blob =>
                {
                    Assert.Equal(zipArtifact, blob.Id);
                    Assert.False(blob.NonShipping);
                    Assert.Collection(blob.Attributes,
                        attr =>
                        {
                            Assert.Equal("ARandomBitOfMAD", attr.Key);
                            Assert.Equal(string.Empty, attr.Value);
                        },
                        attr =>
                        {
                            Assert.Equal("Id", attr.Key);
                            Assert.Equal(zipArtifact, attr.Value);
                        });
                });

            Assert.Collection(model.Artifacts.Packages,
                package =>
                {
                    Assert.Equal("test-package-a", package.Id);
                    Assert.Equal("1.0.0", package.Version);
                    Assert.False(package.NonShipping);
                    Assert.Collection(package.Attributes,
                        attr =>
                        {
                            Assert.Equal("ShouldWePushDaNorpKeg", attr.Key);
                            Assert.Equal("YES", attr.Value);
                        },
                        attr =>
                        {
                            Assert.Equal("Id", attr.Key);
                            Assert.Equal("test-package-a", attr.Value);
                        },
                        attr =>
                        {
                            Assert.Equal("Version", attr.Key);
                            Assert.Equal("1.0.0", attr.Value);
                        });
                });
        }

        /// <summary>
        /// The artifact metadata is parsed as case-insensitive
        /// </summary>
        [Fact]
        public void ArtifactMetadataIsCaseInsensitive()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "ManifestArtifactData", "nonshipping=true;Category=CASE" },
                })
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.Empty(model.Artifacts.Blobs);
            Assert.Collection(model.Artifacts.Packages,
                package =>
                {
                    Assert.Equal("test-package-a", package.Id);
                    Assert.Equal("1.0.0", package.Version);
                    // We used "nonshipping=true" in our artifact metadata
                    Assert.True(package.NonShipping);
                    Assert.True(package.Attributes.ContainsKey("category"));
                    Assert.Collection(package.Attributes,
                        attr =>
                        {
                            // Should have preserved the case on this
                            Assert.Equal("nonshipping", attr.Key);
                            Assert.Equal("true", attr.Value);
                        },
                        attr =>
                        {
                            // Should have preserved the case on this
                            Assert.Equal("Category", attr.Key);
                            Assert.Equal("CASE", attr.Value);
                        },
                        attr =>
                        {
                            Assert.Equal("Id", attr.Key);
                            Assert.Equal("test-package-a", attr.Value);
                        },
                        attr =>
                        {
                            Assert.Equal("Version", attr.Key);
                            Assert.Equal("1.0.0", attr.Value);
                        });
                });
        }

        /// <summary>
        /// We can't create a blob artifact model without a RelativeBlobPath
        /// </summary>
        [Fact]
        public void BlobsWithoutARelativeBlobPathIsInvalid()
        {
            var buildEngine = new MockBuildEngine();
            var stubTask = new StubTask(buildEngine);
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(stubTask);
            const string zipArtifact = "foo/bar/baz/bing.zip";

            var artifacts = new ITaskItem[]
            {
                // Include a package and a fake zip too
                // Note that the relative blob path is a "first class" attribute,
                // not parsed from ManifestArtifactData
                new TaskItem(zipArtifact, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "ARandomBitOfMAD=" },
                }),
            };

            BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.True(taskLoggingHelper.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals($"Missing 'RelativeBlobPath' property on blob {zipArtifact}"));
        }

        /// <summary>
        /// Test that a build without initial location information is rejected
        /// </summary>
        [Fact]
        public void MissingLocationInformationThrowsError()
        {
            var buildEngine = new MockBuildEngine();
            var stubTask = new StubTask(buildEngine);
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(stubTask);
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.zip"));

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=true" }
                })
            };

            BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, null, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.Latest,
                true, taskLoggingHelper);

            // Should have logged an error that an initial location was not present.
            Assert.True(taskLoggingHelper.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals("Missing 'location' property from ManifestBuildData"));
        }

        /// <summary>
        /// Test that a build with initial location attributes in the manifest build data
        /// are accepted.
        /// </summary>
        [Theory]
        [InlineData("Location")]
        [InlineData("InitialAssetsLocation")]
        public void InitialLocationInformationAttributesAreAccepted(string attributeName)
        {
            var buildEngine = new MockBuildEngine();
            var stubTask = new StubTask(buildEngine);
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(stubTask);
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=true" }
                })
            };

            var manifestBuildData = new string[]
            {
                $"{attributeName}={_testInitialLocation}"
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, manifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            // Should have logged an error that an initial location was not present.
            Assert.False(taskLoggingHelper.HasLoggedErrors);

            // Check that the build model has the initial assets location
            Assert.True(model.Identity.Attributes.ContainsKey(attributeName) &&
                model.Identity.Attributes[attributeName] == _testInitialLocation);
        }

        #endregion

        #region Round trip tests

        /// <summary>
        /// Basic round trip from model -> xml -> model has the desired results.
        /// There is already tests for the xml bits of this in the model itself (BuildManifestModel just wraps it
        /// with some file writing).
        /// 
        /// This also tests a few extra cases, like that the additional metadata (e.g. repo uri)
        /// are correctly modeled and preserved.
        /// </summary>
        [Fact]
        public void RoundTripFromTaskItemsToFileToXml()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));

            const string bopSymbolsNupkg = "foo/bar/baz/bop.symbols.nupkg";
            string bobSymbolsExpectedId = $"assets/symbols/{Path.GetFileName(bopSymbolsNupkg)}";
            const string bopSnupkg = "foo/bar/baz/bop.symbols.nupkg";
            string bopSnupkgExpectedId = $"assets/symbols/{Path.GetFileName(bopSnupkg)}";
            const string zipArtifact = "foo/bar/baz/bing.zip";

            var artifacts = new ITaskItem[]
            {
                new TaskItem(bopSymbolsNupkg, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=true;Category=SMORKELER" },
                    { "ThisIsntArtifactMetadata", "YouGoofed!" }
                }),
                new TaskItem(bopSnupkg, new Dictionary<string, string>
                {
                    { "ManifestArtifactData", "NonShipping=false;Category=SNORPKEG;" }
                }),
                // Include a package and a fake zip too
                // Note that the relative blob path is a "first class" attribute,
                // not parsed from ManifestArtifactData
                new TaskItem(zipArtifact, new Dictionary<string, string>
                {
                    { "RelativeBlobPath", zipArtifact },
                    { "ManifestArtifactData", "ARandomBitOfMAD=" },
                }),
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    // This isn't recognized or used for a nupkg
                    { "RelativeBlobPath", zipArtifact },
                    { "ManifestArtifactData", "ShouldWePushDaNorpKeg=YES" },
                })
            };

            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(localPackagePath),
                new TaskItem(zipArtifact)
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "CertificateName", "IHasACert" },
                    { "PublicKeyToken", "BLORG" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "CertificateName", "IHasACert2" }
                })
            };

            var certificatesSignInfo = new ITaskItem[]
            {
                new TaskItem("MyCert", new Dictionary<string, string>()
                {
                    { "DualSigningAllowed", "false" }
                }),
                new TaskItem("MyOtherCert", new Dictionary<string, string>()
                {
                    { "DualSigningAllowed", "true" }
                })
            };

            var fileExtensionSignInfo = new ITaskItem[]
            {
                new TaskItem(".dll", new Dictionary<string, string>()
                {
                    { "CertificateName", "MyCert" }
                })
            };

            string tempXmlFile = System.IO.Path.GetTempFileName();
            try
            {
                var modelFromItems = BuildManifestUtil.CreateModelFromItems(artifacts, itemsToSign,
                    strongNameSignInfo, fileSignInfo, fileExtensionSignInfo, certificatesSignInfo, _testAzdoBuildId,
                    _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, true,
                    VersionTools.BuildManifest.Model.PublishingInfraVersion.Next,
                    false, taskLoggingHelper);

                BuildManifestUtil.CreateBuildManifest(taskLoggingHelper,
                        modelFromItems.Artifacts.Blobs,
                        modelFromItems.Artifacts.Packages,
                        tempXmlFile,
                        modelFromItems.Identity.Name,
                        modelFromItems.Identity.BuildId,
                        modelFromItems.Identity.Branch,
                        modelFromItems.Identity.Commit,
                        modelFromItems.Identity.Attributes.Select(kv => $"{kv.Key}={kv.Value}").ToArray(),
                        bool.Parse(modelFromItems.Identity.IsStable),
                        modelFromItems.Identity.PublishingVersion,
                        bool.Parse(modelFromItems.Identity.IsReleaseOnlyPackageVersion),
                        modelFromItems.SigningInformation);

                // Read the xml file back in and create a model from it.
                var modelFromFile = BuildManifestUtil.ManifestFileToModel(tempXmlFile, taskLoggingHelper);

                // There will be some reordering of the attributes here (they are written to the xml file in
                // a defined order for some properties, then ordered by case).
                // As a result, this comparison isn't exactly the same as some other tests.
                Assert.False(taskLoggingHelper.HasLoggedErrors);
                Assert.Equal(_testAzdoRepoUri, modelFromItems.Identity.Name);
                Assert.Equal(_testAzdoBuildId, modelFromItems.Identity.BuildId);
                Assert.Equal(_testBuildCommit, modelFromItems.Identity.Commit);
                Assert.Equal(VersionTools.BuildManifest.Model.PublishingInfraVersion.Next, modelFromItems.Identity.PublishingVersion);
                Assert.Equal("false", modelFromItems.Identity.IsReleaseOnlyPackageVersion, ignoreCase: true);
                Assert.Equal("true", modelFromItems.Identity.IsStable, ignoreCase: true);
                Assert.Collection(modelFromFile.Artifacts.Blobs,
                    blob =>
                    {
                        Assert.Equal(bobSymbolsExpectedId, blob.Id);
                        Assert.True(blob.NonShipping);
                        Assert.Collection(blob.Attributes,
                            attr => {
                                Assert.Equal("Id", attr.Key);
                                Assert.Equal(bobSymbolsExpectedId, attr.Value);
                            },
                            attr => {
                                Assert.Equal("Category", attr.Key);
                                Assert.Equal("SMORKELER", attr.Value);
                            },
                            attr =>
                            {
                                Assert.Equal("NonShipping", attr.Key);
                                Assert.Equal("true", attr.Value);
                            });
                    },
                    blob =>
                    {
                        Assert.Equal(bopSnupkgExpectedId, blob.Id);
                        Assert.False(blob.NonShipping);
                        Assert.Collection(blob.Attributes,
                            attr => {
                                Assert.Equal("Id", attr.Key);
                                Assert.Equal(bopSnupkgExpectedId, attr.Value);
                            },
                            attr => {
                                Assert.Equal("Category", attr.Key);
                                Assert.Equal("SNORPKEG", attr.Value);
                            },
                            attr =>
                            {
                                Assert.Equal("NonShipping", attr.Key);
                                Assert.Equal("false", attr.Value);
                            });
                    },
                    blob =>
                    {
                        Assert.Equal(zipArtifact, blob.Id);
                        Assert.False(blob.NonShipping);
                        Assert.Collection(blob.Attributes,
                            attr =>
                            {
                                Assert.Equal("Id", attr.Key);
                                Assert.Equal(zipArtifact, attr.Value);
                            },
                            attr =>
                            {
                                Assert.Equal("ARandomBitOfMAD", attr.Key);
                                Assert.Equal(string.Empty, attr.Value);
                            });
                    });

                Assert.Collection(modelFromFile.Artifacts.Packages,
                    package =>
                    {
                        Assert.Equal("test-package-a", package.Id);
                        Assert.Equal("1.0.0", package.Version);
                        Assert.False(package.NonShipping);
                        Assert.Collection(package.Attributes,
                            attr =>
                            {
                                Assert.Equal("Id", attr.Key);
                                Assert.Equal("test-package-a", attr.Value);
                            },
                            attr =>
                            {
                                Assert.Equal("Version", attr.Key);
                                Assert.Equal("1.0.0", attr.Value);
                            },
                            attr =>
                            {
                                Assert.Equal("ShouldWePushDaNorpKeg", attr.Key);
                                Assert.Equal("YES", attr.Value);
                            });
                    });

                Assert.NotNull(modelFromFile.SigningInformation);
                Assert.Collection(modelFromFile.SigningInformation.ItemsToSign,
                    item =>
                    {
                        Assert.Equal("bing.zip", item.Include);
                    },
                    item =>
                    {
                        Assert.Equal("test-package-a.nupkg", item.Include);
                    });
                Assert.Collection(modelFromFile.SigningInformation.StrongNameSignInfo,
                    item =>
                    {
                        Assert.Equal("test-package-a.nupkg", item.Include);
                        Assert.Equal("IHasACert", item.CertificateName);
                        Assert.Equal("BLORG", item.PublicKeyToken);
                    });
                Assert.Collection(modelFromFile.SigningInformation.FileSignInfo,
                    item =>
                    {
                        Assert.Equal("test-package-a.nupkg", item.Include);
                        Assert.Equal("IHasACert2", item.CertificateName);
                    });
                Assert.Collection(modelFromFile.SigningInformation.CertificatesSignInfo,
                    item =>
                    {
                        Assert.Equal("MyCert", item.Include);
                        Assert.Equal("false", item.DualSigningAllowed);
                    },
                    item =>
                    {
                        Assert.Equal("MyOtherCert", item.Include);
                        Assert.Equal("true", item.DualSigningAllowed);
                    });
                Assert.Collection(modelFromFile.SigningInformation.FileExtensionSignInfo,
                    item =>
                    {
                        Assert.Equal(".dll", item.Include);
                        Assert.Equal("MyCert", item.CertificateName);
                    });
            }
            finally
            {
                if (File.Exists(tempXmlFile))
                {
                    File.Delete(tempXmlFile);
                }
            }
        }

        #endregion

        #region Signing Model Information

        /// <summary>
        /// If no signing information is present, we should still get a signing model,
        /// with nothing in it
        /// </summary>
        [Fact]
        public void NoSigningInformationDoesNotThrowAnError()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "ManifestArtifactData", "nonshipping=true;Category=CASE" },
                })
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, null,
                null, null, null, null, _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.False(taskLoggingHelper.HasLoggedErrors);
            Assert.NotNull(model.SigningInformation);
            Assert.Empty(model.SigningInformation.ItemsToSign);
            Assert.Empty(model.SigningInformation.CertificatesSignInfo);
            Assert.Empty(model.SigningInformation.FileExtensionSignInfo);
            Assert.Empty(model.SigningInformation.FileSignInfo);
            Assert.Empty(model.SigningInformation.StrongNameSignInfo);
        }

        /// <summary>
        /// Validate the strong name signing information is correctly propagated to the model
        /// </summary>
        [Fact]
        public void SignInfoIsCorrectlyPopulatedFromItems()
        {
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(new StubTask());
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));
            var zipPath = @"this\is\a\zip.zip";

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()),
                new TaskItem(zipPath, new Dictionary<string, string>()
                {
                    { "RelativeBlobPath", zipPath },
                })
            };

            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(localPackagePath),
                new TaskItem(zipPath)
            };

            var strongNameSignInfo = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "CertificateName", "IHasACert" },
                    { "PublicKeyToken", "BLORG" }
                })
            };

            var fileSignInfo = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()
                {
                    { "CertificateName", "IHasACert2" }
                })
            };

            var certificatesSignInfo = new ITaskItem[]
            {
                new TaskItem("MyCert", new Dictionary<string, string>()
                {
                    { "DualSigningAllowed", "false" }
                }),
                new TaskItem("MyOtherCert", new Dictionary<string, string>()
                {
                    { "DualSigningAllowed", "true" }
                })
            };

            var fileExtensionSignInfo = new ITaskItem[]
            {
                new TaskItem(".dll", new Dictionary<string, string>()
                {
                    { "CertificateName", "MyCert" }
                })
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, itemsToSign,
                strongNameSignInfo, fileSignInfo, fileExtensionSignInfo, certificatesSignInfo,
                _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.False(taskLoggingHelper.HasLoggedErrors);
            Assert.NotNull(model.SigningInformation);
            Assert.Collection(model.SigningInformation.ItemsToSign,
                item =>
                {
                    Assert.Equal("test-package-a.nupkg", item.Include);
                },
                item =>
                {
                    Assert.Equal("zip.zip", item.Include);
                });
            Assert.Collection(model.SigningInformation.StrongNameSignInfo,
                item =>
                {
                    Assert.Equal("test-package-a.nupkg", item.Include);
                    Assert.Equal("IHasACert", item.CertificateName);
                    Assert.Equal("BLORG", item.PublicKeyToken);
                });
            Assert.Collection(model.SigningInformation.FileSignInfo,
                item =>
                {
                    Assert.Equal("test-package-a.nupkg", item.Include);
                    Assert.Equal("IHasACert2", item.CertificateName);
                });
            Assert.Collection(model.SigningInformation.CertificatesSignInfo,
                item =>
                {
                    Assert.Equal("MyCert", item.Include);
                    Assert.Equal("false", item.DualSigningAllowed);
                },
                item =>
                {
                    Assert.Equal("MyOtherCert", item.Include);
                    Assert.Equal("true", item.DualSigningAllowed);
                });
            Assert.Collection(model.SigningInformation.FileExtensionSignInfo,
                item =>
                {
                    Assert.Equal(".dll", item.Include);
                    Assert.Equal("MyCert", item.CertificateName);
                });
        }

        /// <summary>
        /// If a file is in ItemsToSign, it should also be in the artifacts.
        /// </summary>
        [Fact]
        public void ArtifactToSignMustExistInArtifacts()
        {
            var buildEngine = new MockBuildEngine();
            var stubTask = new StubTask(buildEngine);
            var taskLoggingHelper = new Microsoft.Build.Utilities.TaskLoggingHelper(stubTask);
            var localPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.nupkg"));
            const string zipPath = @"this\is\a\zip.zip";
            const string bogusNupkgToSign = "totallyboguspackage.nupkg";

            var artifacts = new ITaskItem[]
            {
                new TaskItem(localPackagePath, new Dictionary<string, string>()),
                new TaskItem(zipPath, new Dictionary<string, string>()
                {
                    { "RelativeBlobPath", zipPath },
                })
            };

            var itemsToSign = new ITaskItem[]
            {
                new TaskItem(bogusNupkgToSign),
            };

            var model = BuildManifestUtil.CreateModelFromItems(artifacts, itemsToSign,
                null, null, null, null,
                _testAzdoBuildId, _defaultManifestBuildData, _testAzdoRepoUri, _testBuildBranch, _testBuildCommit, false,
                VersionTools.BuildManifest.Model.PublishingInfraVersion.All,
                true, taskLoggingHelper);

            Assert.True(taskLoggingHelper.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals($"Item to sign '{bogusNupkgToSign}' was not found in the artifacts"));
        }

        #endregion
    }
}

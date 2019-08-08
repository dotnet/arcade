// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class ValidateHarvestVersionIsLatestForEraTests
    {
        private Log _log;
        private TestBuildEngine _engine;
        private const string TestReportPath = "dummyReport.json";
        private PackageReport _testPackageReport = new PackageReport()
        {
            Id = "TestPackage",
            Version = "4.7.0",
            Targets = new Dictionary<string, Target>
            {
                {
                    "testTarget", new Target
                    {
                        CompileAssets = new PackageAsset[]
                        {
                            new PackageAsset{ HarvestedFrom = "TestPackage/4.6.2/ref/netstandard2.0/TestPackage.dll" }
                        }
                    }
                }
            }
        };

        public ValidateHarvestVersionIsLatestForEraTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
        }

        [Fact]
        public void ValidationFailsWhenHarvestVersionIsNotLatestTest()
        {
            TestableValidateHarvestVersionTask task = new TestableValidateHarvestVersionTask(() => _testPackageReport, (packageId, eraMajor, eraMinor) =>
            {
                return $"{eraMajor}.{eraMinor}.3";
            }) { BuildEngine = _engine, PackageReportPath = TestReportPath };

            _log.Reset();
            task.Execute();
            Assert.Equal(1, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
        }

        [Fact]
        public void ValidationSucceedsWhenHarvestVersionIsLatestTest()
        {
            TestableValidateHarvestVersionTask task = new TestableValidateHarvestVersionTask(() => _testPackageReport, (packageId, eraMajor, eraMinor) =>
            {
                return $"{eraMajor}.{eraMinor}.2";
            })
            { BuildEngine = _engine, PackageReportPath = TestReportPath };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
        }

        [Fact]
        public void ValidationSucceedsWhenNoPackagesWhereHarvestedTest()
        {
            TestableValidateHarvestVersionTask task = new TestableValidateHarvestVersionTask(() =>
            {
                return new PackageReport()
                {
                    Id = "TestPackage",
                    Targets = new Dictionary<string, Target>
                    {
                        {
                            "testTarget", new Target
                            {
                                CompileAssets = new PackageAsset[]
                                {
                                    new PackageAsset{  }
                                },
                                RuntimeAssets = new PackageAsset[]
                                {
                                    new PackageAsset{  }
                                },
                                NativeAssets = new PackageAsset[]
                                {
                                    new PackageAsset{  }
                                }
                            }
                        }
                    }
                };
            }, (packageId, eraMajor, eraMinor) => string.Empty)
            { BuildEngine = _engine, PackageReportPath = TestReportPath };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
        }

        [Fact]
        public void ValidationFailsWhenHarvestingFromCurrentVersionTest()
        {
            TestableValidateHarvestVersionTask task = new TestableValidateHarvestVersionTask(() =>
            {
                return new PackageReport()
                {
                    Id = "TestPackage",
                    Version = "4.6.3",
                    Targets = new Dictionary<string, Target>
                    {
                        {
                            "testTarget", new Target
                            {
                                CompileAssets = new PackageAsset[]
                                {
                                    new PackageAsset{ HarvestedFrom = "TestPackage/4.6.2/ref/netstandard2.0/TestPackage.dll" }
                                }
                            }
                        }
                    }
                };
            }, (packageId, eraMajor, eraMinor) => string.Empty)
            { BuildEngine = _engine, PackageReportPath = TestReportPath };

            _log.Reset();
            task.Execute();
            Assert.Equal(1, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
        }

        private class TestableValidateHarvestVersionTask : ValidateHarvestVersionIsLatestForEra
        {
            private Func<PackageReport> _packageReportFunc;
            private Func<string, int, int, string> _getLatestStableVersionFunc;

            public TestableValidateHarvestVersionTask(Func<PackageReport> packageReportFunc, Func<string, int, int, string> getLatestStableVersionFunc)
            {
                _packageReportFunc = packageReportFunc;
                _getLatestStableVersionFunc = getLatestStableVersionFunc;
            }

            protected override PackageReport GetPackageReportFromPath() => _packageReportFunc();

            protected override string GetLatestStableVersionForEra(string packageId, int eraMajorVersion, int eraMinorVersion) => _getLatestStableVersionFunc(packageId, eraMajorVersion, eraMinorVersion);
        }
    }
}

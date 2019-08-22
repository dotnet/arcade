// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class GetLastStablePackageTests
    {
        private Log _log;
        private TestBuildEngine _engine;
        private ITaskItem[] _packageIndexes;

        public GetLastStablePackageTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
            _packageIndexes = new[] { new TaskItem("packageIndex.json") };
        }

        [Fact]
        public void LaterPreReleaseGetsStable()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.2.0-pre")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.LatestPackages.Length, task.LastStablePackages.Length);
            Assert.Equal("StableVersionTest", task.LastStablePackages[0].ItemSpec);
            Assert.Equal("1.1.0", task.LastStablePackages[0].GetMetadata("Version"));
        }


        [Fact]
        public void StableGetsPreviousStable()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.1.0")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.LatestPackages.Length, task.LastStablePackages.Length);
            Assert.Equal("StableVersionTest", task.LastStablePackages[0].ItemSpec);
            Assert.Equal("1.0.0", task.LastStablePackages[0].GetMetadata("Version"));
        }

        [Fact]
        public void PreGetsPreviousStable()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.1.0-pre")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.LatestPackages.Length, task.LastStablePackages.Length);
            Assert.Equal("StableVersionTest", task.LastStablePackages[0].ItemSpec);
            Assert.Equal("1.0.0", task.LastStablePackages[0].GetMetadata("Version"));
        }

        [Fact]
        public void PriorToStableGetsNothing()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.0.0-pre")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(0, task.LastStablePackages.Length);
        }

        [Fact]
        public void DoNotAllowSameEraPackageVersions()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.1.1-pre")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages,
                DoNotAllowVersionsFromSameRelease = true
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.LatestPackages.Length, task.LastStablePackages.Length);
            Assert.Equal("StableVersionTest", task.LastStablePackages[0].ItemSpec);
            Assert.Equal("1.0.0", task.LastStablePackages[0].GetMetadata("Version"));
        }

        [Fact]
        public void AllowSameEraPackageVersions()
        {
            ITaskItem[] latestPackages = new[]
            {
                CreateItem("StableVersionTest", "1.1.1-pre")
            };

            GetLastStablePackage task = new GetLastStablePackage()
            {
                BuildEngine = _engine,
                PackageIndexes = _packageIndexes,
                LatestPackages = latestPackages
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.LatestPackages.Length, task.LastStablePackages.Length);
            Assert.Equal("StableVersionTest", task.LastStablePackages[0].ItemSpec);
            Assert.Equal("1.1.0", task.LastStablePackages[0].GetMetadata("Version"));
        }

        private static ITaskItem CreateItem(string name, string version)
        {
            TaskItem item = new TaskItem(name);

            if (version != null)
            {
                item.SetMetadata("Version", version);
            }

            return item;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class ApplyBaseLineTests
    {
        private Log _log;
        private TestBuildEngine _engine;
        private ITaskItem[] _packageIndexes;

        public ApplyBaseLineTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);
            _packageIndexes = new[] { new TaskItem("packageIndex.json") };
        }

        [Fact]
        public void ApplyBaseLineLiftToBaseLine()
        {
            ITaskItem[] dependencies = new[]
            {
                CreateItem("System.Runtime", "4.0.0")
            };

            ApplyBaseLine task = new ApplyBaseLine()
            {
                BuildEngine = _engine,
                Apply = true,
                PackageIndexes = _packageIndexes,
                OriginalDependencies = dependencies
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.OriginalDependencies.Length, task.BaseLinedDependencies.Length);
            Assert.Equal("System.Runtime", task.BaseLinedDependencies[0].ItemSpec);
            Assert.Equal("4.0.21", task.BaseLinedDependencies[0].GetMetadata("Version"));
        }

        [Fact]
        public void DontApplyBaseLineIfGreater()
        {

            ITaskItem[] dependencies = new[]
            {
                CreateItem("System.Runtime", "4.1.0")
            };

            ApplyBaseLine task = new ApplyBaseLine()
            {
                BuildEngine = _engine,
                Apply = true,
                PackageIndexes = _packageIndexes,
                OriginalDependencies = dependencies
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.OriginalDependencies.Length, task.BaseLinedDependencies.Length);
            Assert.Equal("System.Runtime", task.BaseLinedDependencies[0].ItemSpec);
            Assert.Equal("4.1.0", task.BaseLinedDependencies[0].GetMetadata("Version"));
        }

        [Fact]
        public void ApplyBaselineToUnversionedDependency()
        {
            ITaskItem[] dependencies = new[]
            {
                CreateItem("System.Runtime", null)
            };

            ApplyBaseLine task = new ApplyBaseLine()
            {
                BuildEngine = _engine,
                Apply = true,
                PackageIndexes = _packageIndexes,
                OriginalDependencies = dependencies
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.OriginalDependencies.Length, task.BaseLinedDependencies.Length);
            Assert.Equal("System.Runtime", task.BaseLinedDependencies[0].ItemSpec);
            Assert.Equal("4.0.21", task.BaseLinedDependencies[0].GetMetadata("Version"));
        }

        [Fact]
        public void ApplyBaselineToUntrackedDependency()
        {

            ITaskItem[] dependencies = new[]
            {
                CreateItem("System.Banana", "4.0.0")
            };

            ApplyBaseLine task = new ApplyBaseLine()
            {
                BuildEngine = _engine,
                Apply = true,
                PackageIndexes = _packageIndexes,
                OriginalDependencies = dependencies
            };

            _log.Reset();
            task.Execute();
            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(task.OriginalDependencies.Length, task.BaseLinedDependencies.Length);
            Assert.Equal("System.Banana", task.BaseLinedDependencies[0].ItemSpec);
            Assert.Equal("4.0.0", task.BaseLinedDependencies[0].GetMetadata("Version"));
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

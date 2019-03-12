using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class HarvestPackageTests
    {
        private Log _log;
        private TestBuildEngine _engine;

        private ITaskItem[] _frameworks;

        public HarvestPackageTests(ITestOutputHelper output)
        {
            _log = new Log(output);
            _engine = new TestBuildEngine(_log);

            _frameworks = new[]
            {
                CreateFrameworkItem("netcoreapp1.0", "win7-x86;win7-x64;osx.10.11-x64;centos.7-x64;debian.8-x64;linuxmint.17-x64;opensuse.13.2-x64;rhel.7.2-x64;ubuntu.14.04-x64;ubuntu.16.04-x64"),
                CreateFrameworkItem("netcoreapp1.1", "win7-x86;win7-x64;osx.10.11-x64;centos.7-x64;debian.8-x64;linuxmint.17-x64;opensuse.13.2-x64;rhel.7.2-x64;ubuntu.14.04-x64;ubuntu.16.04-x64"),

                CreateFrameworkItem("netcore50", "win10-x86;win10-x86-aot;win10-x64;win10-x64-aot;win10-arm;win10-arm-aot"),
                CreateFrameworkItem("netcore45", ""),
                CreateFrameworkItem("netcore451", ""),

                CreateFrameworkItem("net45", ";win-x86;win-x64"),
                CreateFrameworkItem("net451", ";win-x86;win-x64"),
                CreateFrameworkItem("net46", ";win-x86;win-x64;win7-x86;win7-x64"),
                CreateFrameworkItem("net461", ";win-x86;win-x64;win7-x86;win7-x64"),
                CreateFrameworkItem("net462", ";win-x86;win-x64;win7-x86;win7-x64"),
                CreateFrameworkItem("net463", ";win-x86;win-x64;win7-x86;win7-x64"),

                CreateFrameworkItem("wpa81", ""),
                CreateFrameworkItem("wp8", ""),
                CreateFrameworkItem("MonoAndroid10", ""),
                CreateFrameworkItem("MonoTouch10", ""),
                CreateFrameworkItem("xamarinios10", ""),
                CreateFrameworkItem("xamarinmac20", ""),
                CreateFrameworkItem("xamarintvos10", ""),
                CreateFrameworkItem("xamarinwatchos10", "")
            };
        }
        
        private ITaskItem CreateFrameworkItem(string tfm, string rids)
        {
            var item = new TaskItem(tfm);
            item.SetMetadata("RuntimeIDs", rids);
            return item;
        }

        private string FindPackageFolder(string packageId, string packageVersion)
        {
            string result = null;
            
            for (string candidate = Directory.GetCurrentDirectory();
                candidate != Path.GetPathRoot(candidate);
                candidate = Path.GetDirectoryName(candidate))
            {
                string packagesCandidate = Path.Combine(candidate, "packages");

                string packageFolder = Path.Combine(packagesCandidate, packageId, packageVersion);
                string packageFolderLower = Path.Combine(packagesCandidate, packageId.ToLowerInvariant(), packageVersion.ToLowerInvariant());

                if (Directory.Exists(packageFolder) || Directory.Exists(packageFolderLower))
                {
                    result = packagesCandidate;
                    break;
                }
            }

            Assert.NotNull(result);
            return result;
        }

        [Fact]
        public void TestSimpleLibPackage()
        {
            var task = new HarvestPackage()
            {
                BuildEngine = _engine,
                Frameworks = _frameworks,
                HarvestAssets = true,
                IncludeAllPaths = true,
                PackageId = "System.Collections.Immutable",
                PackageVersion = "1.5.0",
                RuntimeFile = "runtime.json"
            };

            task.PackagesFolders = new[] { FindPackageFolder(task.PackageId, task.PackageVersion) };
            
            _log.Reset();
            task.Execute();

            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(8, task.HarvestedFiles.Length);
            var ns10asset = task.HarvestedFiles.FirstOrDefault(f => f.GetMetadata("TargetFramework") == "netstandard1.0" );
            Assert.NotNull(ns10asset);
            Assert.Equal("1.2.3.0", ns10asset.GetMetadata("AssemblyVersion"));
            Assert.Equal(_frameworks.Length, task.SupportedFrameworks.Length);
        }

        [Fact]
        public void TestRefLibPackage()
        {
            var task = new HarvestPackage()
            {
                BuildEngine = _engine,
                Frameworks = _frameworks,
                HarvestAssets = true,
                IncludeAllPaths = true,
                PackageId = "Microsoft.Win32.Registry",
                PackageVersion = "4.3.0",
                RuntimeFile = "runtime.json"
            };
            task.PackagesFolders = new[] { FindPackageFolder(task.PackageId, task.PackageVersion) };

            _log.Reset();
            task.Execute();

            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(17, task.HarvestedFiles.Length);
            var net46asset = task.HarvestedFiles.FirstOrDefault(f => f.GetMetadata("TargetFramework") == "net46" );
            Assert.NotNull(net46asset);
            Assert.Equal("4.0.1.0", net46asset.GetMetadata("AssemblyVersion"));
            Assert.Equal(6, task.SupportedFrameworks.Length);
        }


        [Fact]
        public void TestSplitPackage()
        {
            var task = new HarvestPackage()
            {
                BuildEngine = _engine,
                Frameworks = _frameworks,
                HarvestAssets = true,
                IncludeAllPaths = true,
                PackageId = "System.Runtime",
                PackageVersion = "4.3.0",
                RuntimeFile = "runtime.json",
                RuntimePackages = new []
                {
                    CreateRuntimePackage("runtime.any.System.Runtime", "4.3.0"),
                    CreateRuntimePackage("runtime.aot.System.Runtime", "4.3.0")
                }
            };
            task.PackagesFolders = new[] { FindPackageFolder(task.PackageId, task.PackageVersion) };

            _log.Reset();
            task.Execute();

            Assert.Equal(0, _log.ErrorsLogged);
            Assert.Equal(0, _log.WarningsLogged);
            Assert.Equal(79, task.HarvestedFiles.Length);
            Assert.Contains(task.HarvestedFiles, f => f.GetMetadata("AssemblyVersion") == "4.1.0.0");
            Assert.Equal(_frameworks.Length, task.SupportedFrameworks.Length);
            Assert.All(task.SupportedFrameworks, f => Assert.NotEqual("unknown", f.GetMetadata("Version")));
        }

        private ITaskItem CreateRuntimePackage(string packageId, string version)
        {
            var item = new TaskItem(packageId);
            item.SetMetadata("Version", version);
            return item;
        }
    }
}

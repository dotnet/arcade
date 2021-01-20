// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Linq;
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

            result.Should().NotBeNull();
            return result;
        }

        private ITaskItem CreateRuntimePackage(string packageId, string version)
        {
            var item = new TaskItem(packageId);
            item.SetMetadata("Version", version);
            return item;
        }
    }
}

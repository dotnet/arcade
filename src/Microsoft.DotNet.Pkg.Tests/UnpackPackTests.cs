// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Pkg.Tests
{
    public class UnpackPackTests
    {
        private readonly ITestOutputHelper output;
        private static readonly string simplePkg = GetResourceFilePath("Simple.pkg");
        private static readonly string withAppPkg = GetResourceFilePath("WithApp.pkg");
        private static readonly string simpleInstallerPkg = GetResourceFilePath("SimpleInstaller.pkg");
        private static readonly string withAppInstallerPkg = GetResourceFilePath("WithAppInstaller.pkg");

        private static readonly string pkgToolPath = Path.Combine(
            Path.GetDirectoryName(typeof(UnpackPackTests).Assembly.Location)!,
            "tools",
            "pkg",
            "Microsoft.Dotnet.Pkg.dll");

        private static readonly string[] simplePkgFiles =
        [
            "Bom",
            "PackageInfo",
            Path.Combine("Payload", "Sample.txt")
        ];

        private static readonly string[] withAppPkgFiles =
        [
            "Bom",
            "PackageInfo",
            Path.Combine("Payload", "test.app")
        ];

        private static readonly string[] appFiles =
        [
            Path.Combine("Contents", "Info.plist"),
            Path.Combine("Contents", "MacOS", "main"),
            Path.Combine("Contents", "Resources", "libexample.dylib")
        ];

        private static readonly string[] simpleInstallerFiles =
        [
            "Distribution",
            "Simple.pkg"
        ];

        private static readonly string[] withAppInstallerFiles =
        [
            "Distribution",
            "WithApp.pkg"
        ];

        public UnpackPackTests(ITestOutputHelper output) => this.output = output;

        [MacOSOnlyFact]
        public void UnpackPackSimplePkg()
        {
            string unpackPath = Path.GetTempFileName();
            string packPath = GetTempPkgPath();

            ExecuteWithCleanup(() =>
            {
                Unpack(simplePkg, unpackPath, simplePkgFiles);
                Pack(unpackPath, packPath, simplePkgFiles);
            }, new List<string> { unpackPath, packPath });
        }

        [MacOSOnlyFact]
        public void UnpackPackWithAppPkg()
        {
            string unpackPath = Path.GetTempFileName();
            string packPath = GetTempPkgPath();

            ExecuteWithCleanup(() =>
            {
                Unpack(withAppPkg, unpackPath, withAppPkgFiles);
                Pack(unpackPath, packPath, withAppPkgFiles);
            }, new List<string> { unpackPath, packPath });
        }

        [MacOSOnlyFact]
        public void UnpackPackAppBundle()
        {
            string unpackPkgPath = Path.GetTempFileName();
            string unpackAppPath = Path.GetTempFileName();
            string packAppPath = GetTempAppPath();

            ExecuteWithCleanup(() =>
            {
                Unpack(withAppPkg, unpackPkgPath, withAppPkgFiles);
                Unpack(Path.Combine(unpackPkgPath, "Payload", "test.app"), unpackAppPath, appFiles);
                Pack(unpackAppPath, packAppPath, appFiles);
            }, new List<string> { unpackPkgPath, unpackAppPath });
        }

        [MacOSOnlyFact]
        public void UnpackPackSimpleInstallerPkg()
        {
            string unpackPath = Path.GetTempFileName();
            string packPath = GetTempPkgPath();

            ExecuteWithCleanup(() =>
            {
                Unpack(simpleInstallerPkg, unpackPath, simpleInstallerFiles);
                Pack(unpackPath, packPath, simpleInstallerFiles);
            }, new List<string> { unpackPath, packPath });
        }

        [MacOSOnlyFact]
        public void UnpackPackSimplePkgInSimpleInstallerPkg()
        {
            string unpackInstallerPath = Path.GetTempFileName();
            string unpackComponentPath = Path.GetTempFileName();
            string packInstallerPath = GetTempPkgPath();

            string componentPkgPath = Path.Combine(unpackInstallerPath, "Simple.pkg");

            ExecuteWithCleanup(() =>
            {
                Unpack(simpleInstallerPkg, unpackInstallerPath, simpleInstallerFiles);
                Unpack(componentPkgPath, unpackComponentPath, simplePkgFiles);
                Pack(unpackComponentPath, componentPkgPath, simplePkgFiles);
                Pack(unpackInstallerPath, packInstallerPath, simpleInstallerFiles);
            }, new List<string> { unpackInstallerPath, unpackComponentPath, packInstallerPath });
        }

        [MacOSOnlyFact]
        public void UnpackPackAppBundleAndWithAppPkgInWithAppInstallerPkg()
        {
            string unpackInstallerPath = Path.GetTempFileName();
            string unpackComponentPath = Path.GetTempFileName();
            string unpackAppPath = Path.GetTempFileName();
            string packInstallerPath = GetTempPkgPath();
            
            string componentPkgPath = Path.Combine(unpackInstallerPath, "WithApp.pkg");
            string appPath = Path.Combine(unpackComponentPath, "Payload", "test.app");

            ExecuteWithCleanup(() =>
                {
                    Unpack(withAppInstallerPkg, unpackInstallerPath, withAppInstallerFiles);
                    Unpack(componentPkgPath, unpackComponentPath, withAppPkgFiles);
                    Unpack(appPath, unpackAppPath, appFiles);
                    Pack(unpackAppPath, appPath, appFiles);
                    Pack(unpackComponentPath, componentPkgPath, withAppPkgFiles);
                    Pack(unpackInstallerPath, packInstallerPath, withAppInstallerFiles);
                },
                new List<string> { unpackInstallerPath, unpackComponentPath, unpackAppPath, packInstallerPath });
        }

        private static void ExecuteWithCleanup(Action action, List<string> cleanupPaths)
        {
            try
            {
                action();
            }
            finally
            {
                foreach (string path in cleanupPaths)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }

        private void Unpack(string srcPath, string dstPath, string[] expectedFiles)
        {
            RunPkgProcess(srcPath, dstPath, "unpack").Should().BeTrue();

            Directory.Exists(dstPath).Should().BeTrue();
            
            CompareContent(dstPath, expectedFiles);
        }

        private void Pack(string srcPath, string dstPath, string[] expectedFiles)
        {
            RunPkgProcess(srcPath, dstPath, "pack").Should().BeTrue();

            File.Exists(dstPath).Should().BeTrue();

            // Unpack the packed pkg and verify the content
            string unpackPath = Path.GetTempFileName();
            ExecuteWithCleanup(() =>
            {
                Unpack(dstPath, unpackPath, expectedFiles);
            }, new List<string> { unpackPath });
        }

        private bool RunPkgProcess(string inputPath, string outputPath, string action)
        {
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = "dotnet",
                Arguments = $@"exec ""{pkgToolPath}"" ""{inputPath}"" ""{outputPath}"" {action}",
                UseShellExecute = false,
                RedirectStandardError = true,
            });

            process!.WaitForExit(60000); // 60 seconds
            bool success = process.ExitCode == 0;
            if (!success)
            {
                output.WriteLine($"Error: {process.StandardError.ReadToEnd()}");
            }
            return success;
        }

        private static string GetResourceFilePath(string resourceName)
        {
            return Path.Combine(
                Path.GetDirectoryName(typeof(UnpackPackTests).Assembly.Location)!,
                "Resources",
                resourceName);
        }

        private static string GetTempPkgPath() => $"{Path.GetTempFileName()}.pkg";

        private static string GetTempAppPath() => $"{Path.GetTempFileName()}.app";

        private static void CompareContent(string basePath, string[] expectedFiles)
        {
            string[] actualFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                .Select(f => f.Substring(basePath.Length + 1))
                .ToArray();
            actualFiles.Should().BeEquivalentTo(expectedFiles);
        }
    }
}

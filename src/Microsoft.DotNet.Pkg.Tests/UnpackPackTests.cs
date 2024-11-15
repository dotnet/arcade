// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Microsoft.DotNet.Pkg.Tests
{
    public class UnpackPackTests
    {
        private static string pkgToolPath = Path.Combine(
            Path.GetDirectoryName(typeof(UnpackPackTests).Assembly.Location),
            "tools",
            "pkg",
            "Microsoft.Dotnet.Pkg.dll");

        private static string[] simplePkgFiles = new[]
        {
            Path.Combine("Bom"),
            Path.Combine("PackageInfo"),
            Path.Combine("Payload", "Sample.txt")
        };

        private static string[] withAppPkgFiles = new[]
        {
            Path.Combine("Bom"),
            Path.Combine("PackageInfo"),
            Path.Combine("Payload", "test.app")
        };

        private static string[] appFiles = new[]
        {
            Path.Combine("Contents", "Info.plist"),
            Path.Combine("Contents", "MacOS", "main"),
            Path.Combine("Contents", "Resources", "libexample.dylib"),
        };

        private static string[] simpleInstallerFiles = new[]
        {
            Path.Combine("Distribution"),
            Path.Combine("Simple.pkg"),
        };

        private static string[] withAppInstallerFiles = new[]
        {
            Path.Combine("Distribution"),
            Path.Combine("WithApp.pkg"),
        };
    
        [MacOSOnlyFact]
        public void UnpackSimplePkg()
        {
            string srcPath = GetResourceFilePath("Simple.pkg");
            string dstPath = GetTempFilePath("SimplePkg");
            try
            {
                Unpack(srcPath, dstPath, simplePkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackSimplePkg()
        {
            string srcPath = GetResourceFilePath("Simple.pkg");
            string dstPath = GetTempFilePath("SimplePkg");
            string packPath = GetTempFilePath("PackSimple.pkg");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Pack the package
                Pack(dstPath, packPath, simplePkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(packPath);
            }
        }

        [MacOSOnlyFact]
        public void UnpackPkgWithApp()
        {
            string srcPath = GetResourceFilePath("WithApp.pkg");
            string dstPath = GetTempFilePath("WithAppPkg");
            try
            {
                Unpack(srcPath, dstPath, withAppPkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackPkgWithApp()
        {
            string srcPath = GetResourceFilePath("WithApp.pkg");
            string dstPath = GetTempFilePath("WithAppPkg");
            string packPath = GetTempFilePath("PackWithApp.pkg");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Pack the package
                Pack(dstPath, packPath, withAppPkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(packPath);
            }
        }

        [MacOSOnlyFact]
        public void UnpackAppBundle()
        {
            string srcPath = GetResourceFilePath("WithApp.pkg");
            string dstPath = GetTempFilePath("WithAppPkg");
            string appPath = Path.Combine(dstPath, "Payload", "test.app");
            string appDstPath = GetTempFilePath("TestApp");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Unpack the app bundle
                Unpack(appPath, appDstPath, appFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                Directory.Delete(appDstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackAppBundle()
        {
            string srcPath = GetResourceFilePath("WithApp.pkg");
            string dstPath = GetTempFilePath("WithAppPkg");
            string appPath = Path.Combine(dstPath, "Payload", "test.app");
            string appDstPath = GetTempFilePath("TestApp");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Unpack the app bundle
                Unpack(appPath, appDstPath);

                // Pack the app bundle
                Pack(appDstPath, appPath, appFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                Directory.Delete(appDstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void UnpackSimpleInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            try
            {
                Unpack(srcPath, dstPath, simpleInstallerFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackSimpleInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            string packPath = GetTempFilePath("PackSimpleInstaller.pkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Pack the installer
                Pack(dstPath, packPath, simpleInstallerFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(packPath);
            }
        }

        [MacOSOnlyFact]
        public void UnpackNestedInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            string simplePkgPath = Path.Combine(dstPath, "Simple.pkg");
            string simplePkgDstPath = GetTempFilePath("SimplePkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath, simpleInstallerFiles);

                // Unpack the simple package inside the installer
                Unpack(simplePkgPath, simplePkgDstPath, simplePkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                Directory.Delete(simplePkgDstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackNestedInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            string simplePkgPath = Path.Combine(dstPath, "Simple.pkg");
            string simplePkgDstPath = GetTempFilePath("SimplePkg");
            string packPath = GetTempFilePath("PackSimpleInstaller.pkg");
            string unpackPackPath = GetTempFilePath("UnpackPackSimpleInstallerPkg");
            string unpackPackSimplePkgPath = Path.Combine(unpackPackPath, "Simple.pkg");
            string unpackPackSimplePkgDstPath = GetTempFilePath("UnpackPackSimplePkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Unpack and pack the simple package
                Unpack(simplePkgPath, simplePkgDstPath);
                Pack(simplePkgDstPath, simplePkgPath);

                // Pack the installer
                Pack(dstPath, packPath, simpleInstallerFiles);

                // Unpack the packed simple pkg inside to compare the content
                Unpack(packPath, unpackPackPath);
                Unpack(unpackPackSimplePkgPath, unpackPackSimplePkgDstPath, simplePkgFiles);
            }
            finally
            {
                File.Delete(packPath);
                File.Delete(simplePkgPath);

                Directory.Delete(dstPath, true);
                Directory.Delete(simplePkgDstPath, true);
                Directory.Delete(unpackPackPath, true);
                Directory.Delete(unpackPackSimplePkgDstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void UnpackNestedInstallerWithApp()
        {
            string srcPath = GetResourceFilePath("WithAppInstaller.pkg");
            string dstPath = GetTempFilePath("WithAppInstallerPkg");
            string withAppPkgPath = Path.Combine(dstPath, "WithApp.pkg");
            string withAppPkgDstPath = GetTempFilePath("WithAppPkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath, withAppInstallerFiles);

                // Unpack the app package inside the installer
                Unpack(withAppPkgPath, withAppPkgDstPath, withAppPkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                Directory.Delete(withAppPkgDstPath, true);
            }
        }

        [MacOSOnlyFact]
        public void PackNestedInstallerWithApp()
        {
            string srcPath = GetResourceFilePath("WithAppInstaller.pkg");
            string dstPath = GetTempFilePath("WithAppInstallerPkg");
            string withAppPkgPath = Path.Combine(dstPath, "WithApp.pkg");
            string withAppPkgDstPath = GetTempFilePath("WithAppPkg");
            string packPath = GetTempFilePath("PackWithAppInstaller.pkg");
            string unpackPackPath = GetTempFilePath("UnpackPackWithAppInstallerPkg");
            string unpackPackWithAppPkgPath = Path.Combine(unpackPackPath, "WithApp.pkg");
            string unpackPackWithAppPkgDstPath = GetTempFilePath("UnpackPackWithAppPkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Unpack and pack the app package
                Unpack(withAppPkgPath, withAppPkgDstPath);
                Pack(withAppPkgDstPath, withAppPkgPath);

                // Pack the installer
                Pack(dstPath, packPath, withAppInstallerFiles);

                // Unpack the packed app pkg inside to compare the content
                Unpack(packPath, unpackPackPath);
                Unpack(unpackPackWithAppPkgPath, unpackPackWithAppPkgDstPath, withAppPkgFiles);
            }
            finally
            {
                File.Delete(packPath);
                File.Delete(withAppPkgPath);

                Directory.Delete(dstPath, true);
                Directory.Delete(withAppPkgDstPath, true);
                Directory.Delete(unpackPackPath, true);
                Directory.Delete(unpackPackWithAppPkgDstPath, true);
            }
        }

        private static void Unpack(string inputPath, string outputPath, string[] expectedFiles = null)
        {
            bool success = RunPkgProcess(inputPath, outputPath, "unpack");
            success.Should().BeTrue();

            Directory.Exists(outputPath).Should().BeTrue();
            
            if (expectedFiles != null)
            {
                CompareContent(outputPath, expectedFiles);
            }
        }

        private static void Pack(string inputPath, string outputPath, string[] expectedFiles = null)
        {
            bool success = RunPkgProcess(inputPath, outputPath, "pack");
            success.Should().BeTrue();

            File.Exists(outputPath).Should().BeTrue();

            if (expectedFiles != null)
            {
                string tempPath = GetTempFilePath($"UnpackPack{Path.GetFileNameWithoutExtension(inputPath)}");
                RunPkgProcess(outputPath, tempPath, "unpack").Should().BeTrue();
                Directory.Exists(tempPath).Should().BeTrue();

                CompareContent(tempPath, expectedFiles);

                Directory.Delete(tempPath, true);
            }
        }

        private static bool RunPkgProcess(string inputPath, string outputPath, string action)
        {
            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = "dotnet",
                Arguments = $@"exec ""{pkgToolPath}"" ""{inputPath}"" ""{outputPath}"" {action}",
                UseShellExecute = false,
                RedirectStandardError = true,
            });

            process.WaitForExit();
            bool success = process.ExitCode == 0;
            if (!success)
            {
                Console.WriteLine($"Error: {process.StandardError.ReadToEnd()}");
            }
            return success;
        }

        private static string GetResourceFilePath(string resourceName)
        {
            return Path.Combine(
                Path.GetDirectoryName(typeof(UnpackPackTests).Assembly.Location),
                "Resources",
                resourceName);
        }

        private static string GetTempFilePath(string fileName)
        {
            return Path.Combine(
                Path.GetTempPath(),
                fileName);
        }

        private static void CompareContent(string basePath, string[] expectedFiles)
        {
            string[] actualFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                .Select(f => f.Substring(basePath.Length + 1))
                .ToArray();
            actualFiles.Should().BeEquivalentTo(expectedFiles);
        }
    }
}

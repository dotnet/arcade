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
    public class UnpackRepackTests
    {
        private static string pkgToolPath = Path.Combine(
            Path.GetDirectoryName(typeof(UnpackRepackTests).Assembly.Location),
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
        public void RepackSimplePkg()
        {
            string srcPath = GetResourceFilePath("Simple.pkg");
            string dstPath = GetTempFilePath("SimplePkg");
            string repackPath = GetTempFilePath("RepackSimple.pkg");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Repack the package
                Repack(dstPath, repackPath, simplePkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(repackPath);
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
        public void RepackPkgWithApp()
        {
            string srcPath = GetResourceFilePath("WithApp.pkg");
            string dstPath = GetTempFilePath("WithAppPkg");
            string repackPath = GetTempFilePath("RepackWithApp.pkg");
            try
            {
                // Unpack the package
                Unpack(srcPath, dstPath);

                // Repack the package
                Repack(dstPath, repackPath, withAppPkgFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(repackPath);
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
        public void RepackAppBundle()
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

                // Repack the app bundle
                Repack(appDstPath, appPath, appFiles);
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
        public void RepackSimpleInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            string repackPath = GetTempFilePath("RepackSimpleInstaller.pkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Repack the installer
                Repack(dstPath, repackPath, simpleInstallerFiles);
            }
            finally
            {
                Directory.Delete(dstPath, true);
                File.Delete(repackPath);
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
        public void RepackNestedInstaller()
        {
            string srcPath = GetResourceFilePath("SimpleInstaller.pkg");
            string dstPath = GetTempFilePath("SimpleInstallerPkg");
            string simplePkgPath = Path.Combine(dstPath, "Simple.pkg");
            string simplePkgDstPath = GetTempFilePath("SimplePkg");
            string repackPath = GetTempFilePath("RepackSimpleInstaller.pkg");
            string unpackRepackPath = GetTempFilePath("UnpackRepackSimpleInstallerPkg");
            string unpackRepackSimplePkgPath = Path.Combine(unpackRepackPath, "Simple.pkg");
            string unpackRepackSimplePkgDstPath = GetTempFilePath("UnpackRepackSimplePkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Unpack and repack the simple package
                Unpack(simplePkgPath, simplePkgDstPath);
                Repack(simplePkgDstPath, simplePkgPath);

                // Repack the installer
                Repack(dstPath, repackPath, simpleInstallerFiles);

                // Unpack the repacked simple pkg inside to compare the content
                Unpack(repackPath, unpackRepackPath);
                Unpack(unpackRepackSimplePkgPath, unpackRepackSimplePkgDstPath, simplePkgFiles);
            }
            finally
            {
                File.Delete(repackPath);
                File.Delete(simplePkgPath);

                Directory.Delete(dstPath, true);
                Directory.Delete(simplePkgDstPath, true);
                Directory.Delete(unpackRepackPath, true);
                Directory.Delete(unpackRepackSimplePkgDstPath, true);
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
        public void RepackNestedInstallerWithApp()
        {
            string srcPath = GetResourceFilePath("WithAppInstaller.pkg");
            string dstPath = GetTempFilePath("WithAppInstallerPkg");
            string withAppPkgPath = Path.Combine(dstPath, "WithApp.pkg");
            string withAppPkgDstPath = GetTempFilePath("WithAppPkg");
            string repackPath = GetTempFilePath("RepackWithAppInstaller.pkg");
            string unpackRepackPath = GetTempFilePath("UnpackRepackWithAppInstallerPkg");
            string unpackRepackWithAppPkgPath = Path.Combine(unpackRepackPath, "WithApp.pkg");
            string unpackRepackWithAppPkgDstPath = GetTempFilePath("UnpackRepackWithAppPkg");
            try
            {
                // Unpack the installer
                Unpack(srcPath, dstPath);

                // Unpack and repack the app package
                Unpack(withAppPkgPath, withAppPkgDstPath);
                Repack(withAppPkgDstPath, withAppPkgPath);

                // Repack the installer
                Repack(dstPath, repackPath, withAppInstallerFiles);

                // Unpack the repacked app pkg inside to compare the content
                Unpack(repackPath, unpackRepackPath);
                Unpack(unpackRepackWithAppPkgPath, unpackRepackWithAppPkgDstPath, withAppPkgFiles);
            }
            finally
            {
                File.Delete(repackPath);
                File.Delete(withAppPkgPath);

                Directory.Delete(dstPath, true);
                Directory.Delete(withAppPkgDstPath, true);
                Directory.Delete(unpackRepackPath, true);
                Directory.Delete(unpackRepackWithAppPkgDstPath, true);
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

        private static void Repack(string inputPath, string outputPath, string[] expectedFiles = null)
        {
            bool success = RunPkgProcess(inputPath, outputPath, "repack");
            success.Should().BeTrue();

            File.Exists(outputPath).Should().BeTrue();

            if (expectedFiles != null)
            {
                string tempPath = GetTempFilePath($"UnpackRepack{Path.GetFileNameWithoutExtension(inputPath)}");
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
                Path.GetDirectoryName(typeof(UnpackRepackTests).Assembly.Location),
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.DotNet.SignCheck;

namespace Microsoft.DotNet.SignCheck.Tests
{
    public class SignCheckTests : IDisposable
    {
        private readonly string _tmpDir;

        public SignCheckTests()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tmpDir);
        }
        public void Dispose()
        {
            try
            {
                Directory.Delete(_tmpDir, recursive: true);
            }
            catch
            {
            }
        }

        private string GetSignToolResourcePath(string name, string relativePath = null)
        {
            var srcPath = Path.Combine(Path.GetDirectoryName(typeof(SignCheckTests).Assembly.Location), "Resources", name);

            var dstDir = _tmpDir;

            if (relativePath != null)
            {
                dstDir = Path.Combine(dstDir, relativePath);
                Directory.CreateDirectory(dstDir);
            }

            var dstPath = Path.Combine(dstDir, name);

            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath);
            }

            return dstPath;
        }

#if NETFRAMEWORK
        [Fact]
        public void ValidateGetInputFilesFromOptionsEmptyList()
        {
            SignCheck signCheck = new SignCheck(new[] { "" });

            signCheck.InputFiles.Should().BeEmpty();
            signCheck.Log.HasLoggedErrors.Should().BeFalse();
        }
        [Fact]
        public void ValidateFailureForMissingFile()
        {
            SignCheck signCheck = new SignCheck(new[] { "--input-files", "notarealfile.dll" });
            signCheck.InputFiles.Should().BeEmpty();
            signCheck.Log.HasLoggedErrors.Should().BeTrue();
        }
        [Fact]
        public void ValidateGetInputFilesFromOptionsPaths()
        {
            // Load resources
            GetSignToolResourcePath("SignedLibrary.dll");
            GetSignToolResourcePath("SameFiles1.zip");
            GetSignToolResourcePath("SameFiles2.zip");
            GetSignToolResourcePath("Simple.exe");
            GetSignToolResourcePath("Simple.nupkg");

            SignCheck signCheck = new SignCheck(new[] {
                "--input-files",
                Path.Combine(_tmpDir, "SignedLibrary.dll"),
                Path.Combine(_tmpDir, "SameFiles?.zip"),
                Path.Combine(_tmpDir, "Simple.*")
            }); ;

            var expected = new[] {
                Path.Combine(_tmpDir, "SignedLibrary.dll"),
                Path.Combine(_tmpDir, "SameFiles1.zip"),
                Path.Combine(_tmpDir, "SameFiles2.zip"),
                Path.Combine(_tmpDir, "Simple.exe"),
                Path.Combine(_tmpDir, "Simple.nupkg")
            };
            signCheck.InputFiles.Should().BeEquivalentTo(expected);
            signCheck.Log.HasLoggedErrors.Should().BeFalse();
        }

        [Fact]
        public void ValidateGetInputFilesFromOptionsDirectories()
        {
            // Load resources
            GetSignToolResourcePath("SameFiles1.zip", "a");
            GetSignToolResourcePath("SameFiles2.zip", "b");

            SignCheck signCheck = new SignCheck(new[] {
                "--input-files",
                Path.Combine(_tmpDir, "*", "SameFiles*")
            }); ;

            var expected = new[] {
                Path.Combine(_tmpDir, "a", "SameFiles1.zip"),
                Path.Combine(_tmpDir, "b", "SameFiles2.zip"),
            };
            signCheck.InputFiles.Should().BeEquivalentTo(expected);
            signCheck.Log.HasLoggedErrors.Should().BeFalse();
        }

        // bool EnableJarSignatureVerification
        // bool EnableXmlSignatureVerification
        // string FileStatus
        // string ExclusionsOutput
        // bool SkipTimestamp
        // bool Recursive
        // bool VerifyStrongName
        // string ExclusionsFile
        // ITaskItem[] InputFiles
        // string LogFile
        // string ErrorLogFile
        // string Verbosity
        // string ArtifactFolder
        [Fact]
        public void ValidateSignToolTaskParsesOptions()
        {
            var resourceFile = GetSignToolResourcePath("SignedLibrary.dll");

            var buildEngine = new MockBuildEngine();
            var task = new SignCheckTask
            {
                BuildEngine = buildEngine,
                EnableJarSignatureVerification = true,
                EnableXmlSignatureVerification = true,
                FileStatus = "AllFiles",
                ExclusionsOutput = Path.Combine(_tmpDir, "exclusionsoutput.log"),
                SkipTimestamp = true,
                Recursive = true,
                VerifyStrongName = true,
                ExclusionsFile = "nonexistantexclusionsfile.txt",
                InputFiles = new ITaskItem[] { new TaskItem(resourceFile), new TaskItem("a.dll") },
                LogFile = "logfile.log",
                ErrorLogFile = "errorlogfile.log",
                Verbosity = "Diagnostic",
                ArtifactFolder = _tmpDir
            };
            task.Execute();
            PrivateObject privateTask = new PrivateObject(task);
            SignCheck signCheck = (SignCheck) privateTask.GetFieldOrProperty("_signCheck");

            // validate signcheck object
            signCheck.HasArgErrors.Should().BeFalse();
            signCheck.InputFiles.Should().BeEquivalentTo(new[] { resourceFile });
            signCheck.Log.HasLoggedErrors.Should().BeFalse();
            
            // validate signcheck options object
            Options options = signCheck.Options;
            options.EnableJarSignatureVerification.Should().BeTrue();
            options.EnableXmlSignatureVerification.Should().BeTrue();
            options.ErrorLogFile.Should().Be("errorlogfile.log");
            options.ExclusionsFile.Should().Be("nonexistantexclusionsfile.txt");
            options.LogFile.Should().Be("logfile.log");
            options.Recursive.Should().BeTrue();
            options.SkipTimestamp.Should().BeTrue();
            options.TraverseSubFolders.Should().BeTrue();
            options.Verbosity.ToString().Should().Be(LoggerVerbosity.Diagnostic.ToString());
            options.VerifyStrongName.Should().BeTrue();
        }
#endif
    }
}

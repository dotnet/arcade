// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class CollectTestResultsTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Collector_StagesNestedResultsWithoutDuplicatingUploadRoot(bool uploadInsideWorkingDirectory)
    {
        string root = Path.Combine(Path.GetTempPath(), "helix-collector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string work = Path.Combine(root, "work");
            string upload = Path.Combine(root, uploadInsideWorkingDirectory ? Path.Combine("work", "upload") : "upload");
            string nested = Path.Combine(work, "one", "two", "three", "four", "five", "six", "space and 'quote'");
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(upload);
            string[] fileNames = ["testResults.xml", "prefix.TEST-RESULTS.XML", "result.TRX", "junit-results.xml"];
            foreach (string name in fileNames)
            {
                File.WriteAllText(Path.Combine(nested, name), name);
            }
            File.WriteAllText(Path.Combine(nested, "console.log"), "not a result");
            File.WriteAllText(Path.Combine(upload, "already.trx"), "already uploaded");

            using Process collector = StartCollector(work, upload);
            string output = await collector.StandardOutput.ReadToEndAsync();
            string error = await collector.StandardError.ReadToEndAsync();
            await collector.WaitForExitAsync();

            Assert.True(collector.ExitCode == 0, output + error);
            foreach (string name in fileNames)
            {
                string destination = Path.Combine(upload, Path.GetRelativePath(work, nested), name);
                Assert.Equal(name, File.ReadAllText(destination));
            }
            Assert.Equal(fileNames.Length + 1, Directory.GetFiles(upload, "*", SearchOption.AllDirectories).Length);
            Assert.Equal("already uploaded", File.ReadAllText(Path.Combine(upload, "already.trx")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Collector_FailsWhenUploadRootIsMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "helix-collector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using Process collector = StartCollector(root, upload: null);
            string error = await collector.StandardError.ReadToEndAsync();
            await collector.WaitForExitAsync();
            Assert.NotEqual(0, collector.ExitCode);
            Assert.Contains("HELIX_WORKITEM_UPLOAD_ROOT", error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Process StartCollector(string workingDirectory, string upload)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell" : "sh")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (OperatingSystem.IsWindows())
        {
            foreach (string arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File" })
            {
                start.ArgumentList.Add(arg);
            }
        }
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "SdkTools", "test-results",
            OperatingSystem.IsWindows() ? "collect-test-results.ps1" : "collect-test-results.sh"));
        start.Environment.Remove("HELIX_WORKITEM_UPLOAD_ROOT");
        if (upload != null)
        {
            start.Environment["HELIX_WORKITEM_UPLOAD_ROOT"] = upload;
        }
        return Process.Start(start);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.DotNet.XHarness.Android;

public static class ApkHelper
{
    public static List<string> GetApkSupportedArchitectures(string apkPath)
    {
        if (string.IsNullOrEmpty(apkPath))
        {
            throw new ArgumentException("Please supply a value for apkPath");
        }
        if (!File.Exists(apkPath))
        {
            throw new FileNotFoundException($"Invalid APK Path: '{apkPath}'", apkPath);
        }
        if (!Path.GetExtension(apkPath).Equals(".apk", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only know how to open APK files.");
        }

        using (ZipArchive archive = ZipFile.Open(apkPath, ZipArchiveMode.Read))
        {
            // Enumerate all folders under /lib inside the zip
            var allLibFolders = archive.Entries.Where(e => e.FullName.StartsWith("lib/"))
                                               .Select(e => e.FullName[4..e.FullName.IndexOf('/', 4)])
                                               .Distinct().ToList();

            return allLibFolders;
        }
    }
}

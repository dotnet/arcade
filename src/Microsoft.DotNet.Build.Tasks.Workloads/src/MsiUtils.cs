// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Package;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class MsiUtils
    {
        private const string _getFilesQuery = "SELECT `File`, `Component_`, `FileName`, `FileSize`, `Version`, `Language`, `Attributes`, `Sequence` FROM `File`";

        public static IEnumerable<FileRow> GetAllFiles(string packagePath)
        {
            using InstallPackage ip = new(packagePath, DatabaseOpenMode.ReadOnly);
            using Database db = new(packagePath, DatabaseOpenMode.ReadOnly);
            using View fileView = db.OpenView(_getFilesQuery);
            List<FileRow> files = new();
            fileView.Execute();

            foreach (Record file in fileView)
            {
                files.Add(FileRow.Create(file, ip.Files[(string)file["File"]].TargetPath));
            }

            return files;
        }

        public static string GetProperty(string packagePath, string property)
        {
            using InstallPackage ip = new(packagePath, DatabaseOpenMode.ReadOnly);
            return ip.Property[property];
            
        }
        /// <summary>
        /// Calculate the number of bytes a Windows Installer Package would consume on disk. The function assumes that all files will be installed.
        /// </summary>
        /// <param name="packagePath">The path of the installation package.</param>
        /// <param name="factor">Multiplication factor to use to account for additional space requirements, e.g. registry entries for components in the installer database.</param>
        /// <returns></returns>
        public static long GetInstallSize(string packagePath, double factor = 1.4)
        {
            var files = GetAllFiles(packagePath);
            return files.Sum(f => Convert.ToInt64(f.FileSize * factor));
        }
    }
}

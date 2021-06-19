// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Represents a NuGet package that can be harvested to generate an MSI.
    /// </summary>
    public class NugetPackage
    {
        /// <summary>
        /// The package authors.
        /// </summary>
        public string Authors
        {
            get;
        }

        public string Copyright
        {
            get;
        }

        public string Description
        {
            get;
        }

        /// <summary>
        /// The NuGet package identifier.
        /// </summary>
        public string Id => Identity.Id;

        /// <summary>
        /// The identity of the NuGet package.
        /// </summary>
        public PackageIdentity Identity
        {
            get;
        }

        public LicenseMetadata LicenseData
        {
            get;
        }

        public string LicenseUrl
        {
            get;
        }

        public string PackagePath
        {
            get;
        }

        public string ProductVersion => $"{Version.Major}.{Version.Minor}.{Version.Patch}";

        public string Title
        {
            get;
        }

        /// <summary>
        /// The version of the NuGet package.
        /// </summary>
        public NuGetVersion Version => Identity.Version;

        private TaskLoggingHelper Log;

        public NugetPackage(string packagePath, TaskLoggingHelper log)
        {
            Utils.CheckNullOrEmpty(nameof(packagePath), packagePath);
            PackagePath = packagePath;
            Log = log;

            using FileStream packageFileStream = new(PackagePath, FileMode.Open);
            using PackageArchiveReader packageReader = new(packageFileStream);
            NuspecReader nuspecReader = new(packageReader.GetNuspec());

            Identity = nuspecReader.GetIdentity();
            Title = nuspecReader.GetTitle();
            Authors = nuspecReader.GetAuthors();
            LicenseUrl = nuspecReader.GetLicenseUrl();
            Description = nuspecReader.GetDescription();
            Copyright = nuspecReader.GetCopyright();
            LicenseData = nuspecReader.GetLicenseMetadata();
        }

        /// <summary>
        /// Extract the package contents to the specified directory. Standard metadata will be deleted, e.g. _rels folder, .nuspec file, etc.
        /// </summary>
        /// <param name="destinationDirectory">The directory where the package will be extracted.</param>
        public void Extract(string destinationDirectory, IEnumerable<string> exclusionPatterns)
        {
            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }
            Directory.CreateDirectory(destinationDirectory);
            ZipFile.ExtractToDirectory(PackagePath, destinationDirectory);

            // Remove unnecessary files and directories
            Utils.DeleteDirectory(Path.Combine(destinationDirectory, "_rels"));
            Utils.DeleteDirectory(Path.Combine(destinationDirectory, "package"));

            Utils.DeleteFile(Path.Combine(destinationDirectory, ".signature.p7s"));
            Utils.DeleteFile(Path.Combine(destinationDirectory, "[Content_Types].xml"));
            Utils.DeleteFile(Path.Combine(destinationDirectory, $"{Id}.nuspec"));

            if (exclusionPatterns.Count() > 0)
            {
                IEnumerable<string> allFiles = Directory.EnumerateFiles(destinationDirectory, "*.*", SearchOption.AllDirectories);
                IEnumerable<string> filesToDelete = allFiles.Where(f => exclusionPatterns.Any(p => Regex.IsMatch(f, p)));

                Log?.LogMessage(MessageImportance.High, $"Found {filesToDelete.Count()} files matching exclusion patterns.");

                foreach (string file in filesToDelete)
                {
                    Log?.LogMessage(MessageImportance.High, $"Deleting '{file}'.");
                    File.Delete(file);
                }
            }
        }
    }
}

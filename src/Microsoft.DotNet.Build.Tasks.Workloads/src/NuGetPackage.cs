// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.IO.Compression;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Represents a NuGet package that can be harvested to generate an MSI.
    /// </summary>
    public class NugetPackage
    {
        /// <summary>
        /// The UUID namespace to use for generating a product code.
        /// </summary>
        public static readonly Guid ProductCodeNamespaceUuid = Guid.Parse("3B04DD8B-41C4-4DA3-9E49-4B69F11533A7");

        /// <summary>
        /// The UUID namesapce to use for generating an upgrade code.
        /// </summary>
        public static readonly Guid UpgradeCodeNamespaceUuid = Guid.Parse("C743F81B-B3B5-4E77-9F6D-474EFF3A722C");

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

        public string PackagePath
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

        /// <summary>
        /// Generate a set of preprocessor variable definitions using the metadata.
        /// </summary>
        /// <returns>An enumerable containing package metadata converted to WiX preprocessor definitions.</returns>
        public IEnumerable<string> GetPreprocessorDefinitions()
        {
            yield return $@"PackageId={Id}";
            yield return $@"PackageVersion={Version}";
            yield return $@"ProductVersion={Version.Major}.{Version.Minor}.{Version.Patch}";
            yield return $@"ProductCode={Utils.CreateUuid(ProductCodeNamespaceUuid, Identity.ToString()):B}";
            yield return $@"UpgradeCode={Utils.CreateUuid(UpgradeCodeNamespaceUuid, Identity.ToString()):B}";
        }
    }
}

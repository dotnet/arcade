// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.FileCatalog;

/// <summary>
/// Generates a Windows catalog (.cat) file covering a set of files, in pure managed code
/// (no <c>makecat.exe</c> / Windows SDK required). The catalog is unsigned and ready to be
/// Authenticode-signed by the Arcade signing infrastructure (via <c>FileExtensionSignInfo</c>).
/// </summary>
[MSBuildMultiThreadableTask]
public class GenerateFileCatalog : Task, IMultiThreadableTask
{
    /// <summary>Injected by MSBuild so paths resolve against the project directory in multithreaded builds.</summary>
    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

    /// <summary>The files to include in the catalog.</summary>
    [Required]
    public ITaskItem[] Files { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>Full path of the .cat file to write.</summary>
    [Required]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        if (string.IsNullOrEmpty(OutputPath))
        {
            Log.LogError("OutputPath must be specified.");
            return false;
        }

        FileInfo outputFile = new(TaskEnvironment.GetAbsolutePath(OutputPath));

        if (Files is null || Files.Length == 0)
        {
            Log.LogWarning("No files were provided - skipping catalog generation for '{0}'.", OutputPath);

            // Remove any stale catalog from a previous run so incremental builds don't
            // package a catalog describing files that are no longer present.
            if (File.Exists(outputFile.FullName))
            {
                File.Delete(outputFile.FullName);
            }

            return true;
        }

        var builder = new CatalogBuilder();
        foreach (ITaskItem file in Files)
        {
            // Prefer the computed FullPath, but fall back to ItemSpec for custom ITaskItem
            // implementations that don't populate FullPath metadata.
            string path = file.GetMetadata("FullPath");
            if (string.IsNullOrEmpty(path))
            {
                path = file.ItemSpec;
            }

            FileInfo inputFile = new(TaskEnvironment.GetAbsolutePath(path));
            if (!File.Exists(inputFile.FullName))
            {
                Log.LogError("File not found: '{0}'.", path);
                return false;
            }

            builder.AddFile(inputFile);
        }

        // Create via DirectoryInfo rather than Directory.CreateDirectory(string) so no path
        // string is re-resolved; both create all intermediate directories.
        outputFile.Directory?.Create();

        builder.WriteTo(outputFile);
        Log.LogMessage(MessageImportance.High, "Generated catalog with {0} file(s): {1}", Files.Length, OutputPath);
        return !Log.HasLoggedErrors;
    }
}

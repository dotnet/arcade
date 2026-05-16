// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Generates a catalog (.cat) file for .js files in a directory.
    /// JS files are customer-modifiable runtime/toolchain files that cannot be
    /// directly Authenticode-signed. A .cat catalog provides integrity verification
    /// without preventing modification. The .cat file is then signed by the Arcade
    /// signing infrastructure via FileExtensionSignInfo.
    /// </summary>
    internal static class CatalogFileGenerator
    {
        /// <summary>
        /// Generates a .cat catalog file covering all .js files in the specified directory.
        /// The .cat file is placed in the root of the directory so WiX Heat will include it
        /// in the MSI alongside the .js files.
        /// </summary>
        /// <param name="sourceDirectory">The directory to search for .js files.</param>
        /// <param name="catalogName">The base name for the catalog file (without extension).</param>
        /// <param name="log">Optional logger for diagnostic messages.</param>
        /// <returns>The path to the generated .cat file, or null if no .js files were found or makecat.exe is unavailable.</returns>
        public static string? GenerateCatalog(string sourceDirectory, string catalogName, TaskLoggingHelper? log = null)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                return null;
            }

            string[] jsFiles = Directory.GetFiles(sourceDirectory, "*.js", SearchOption.AllDirectories);
            if (jsFiles.Length == 0)
            {
                return null;
            }

            string? makecatPath = FindMakecat();
            if (makecatPath == null)
            {
                log?.LogMessage(MessageImportance.Normal,
                    "makecat.exe not found. Skipping catalog generation for .js files. " +
                    "Catalog signing requires the Windows SDK.");
                return null;
            }

            string catOutputPath = Path.Combine(sourceDirectory, $"{catalogName}.cat");
            string cdfPath = Path.ChangeExtension(catOutputPath, ".cdf");

            // Generate the CDF (Catalog Definition File)
            using (StreamWriter writer = new(cdfPath))
            {
                writer.WriteLine("[CatalogHeader]");
                writer.WriteLine($"Name={catOutputPath}");
                writer.WriteLine("CatalogVersion=2");
                writer.WriteLine("HashAlgorithms=SHA256");
                writer.WriteLine();
                writer.WriteLine("[CatalogFiles]");

                int index = 0;
                foreach (string jsFile in jsFiles)
                {
                    string fileName = Path.GetFileName(jsFile);
                    // Use a sanitized label: remove non-alphanumeric chars except dots and hyphens
                    string label = $"js_{index}_{SanitizeLabel(fileName)}";
                    writer.WriteLine($"<hash>{label}={jsFile}");
                    index++;
                }
            }

            log?.LogMessage(MessageImportance.Low,
                $"Generated CDF with {jsFiles.Length} .js files at {cdfPath}");

            // Run makecat.exe to produce the .cat file
            ProcessStartInfo psi = new()
            {
                FileName = makecatPath,
                Arguments = $"\"{cdfPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                log?.LogWarning($"makecat.exe failed with exit code {process.ExitCode}. " +
                    $"stdout: {stdout} stderr: {stderr}");
                return null;
            }

            if (!File.Exists(catOutputPath))
            {
                log?.LogWarning($"makecat.exe completed but catalog file was not created: {catOutputPath}");
                return null;
            }

            log?.LogMessage(MessageImportance.Normal,
                $"Generated catalog file covering {jsFiles.Length} .js files: {catOutputPath}");

            // Clean up the CDF file - it's not needed in the MSI
            try { File.Delete(cdfPath); } catch { /* best effort */ }

            return catOutputPath;
        }

        /// <summary>
        /// Finds makecat.exe from the Windows SDK.
        /// </summary>
        private static string? FindMakecat()
        {
            // Try PATH first
            string? pathResult = FindInPath("makecat.exe");
            if (pathResult != null) return pathResult;

            // Search Windows SDK locations
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string sdkRoot = Path.Combine(programFilesX86, "Windows Kits", "10", "bin");

            if (Directory.Exists(sdkRoot))
            {
                // Find the latest version's x64 directory
                return Directory.GetFiles(sdkRoot, "makecat.exe", SearchOption.AllDirectories)
                    .Where(f => f.Contains("x64", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f)
                    .FirstOrDefault();
            }

            return null;
        }

        private static string? FindInPath(string fileName)
        {
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (pathVar == null) return null;

            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                string fullPath = Path.Combine(dir, fileName);
                if (File.Exists(fullPath)) return fullPath;
            }

            return null;
        }

        private static string SanitizeLabel(string name) =>
            new(name.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_').ToArray());
    }
}

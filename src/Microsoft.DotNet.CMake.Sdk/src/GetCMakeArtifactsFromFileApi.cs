// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Microsoft.DotNet.CMake.Sdk
{
    /// <summary>
    /// Reads CMake File API response to find artifacts for a specific source directory.
    /// </summary>
    public class GetCMakeArtifactsFromFileApi : Task
    {
        /// <summary>
        /// The CMake build output directory containing the File API response.
        /// </summary>
        [Required]
        public string CMakeOutputDir { get; set; }

        /// <summary>
        /// The source directory of the CMakeLists.txt to find artifacts for.
        /// </summary>
        [Required]
        public string SourceDirectory { get; set; }

        /// <summary>
        /// The configuration name (e.g., Debug, Release).
        /// </summary>
        [Required]
        public string Configuration { get; set; }

        /// <summary>
        /// Output: The list of artifact file paths.
        /// </summary>
        [Output]
        public ITaskItem[] Artifacts { get; set; }

        public override bool Execute()
        {
            try
            {
                string replyDir = Path.Combine(CMakeOutputDir, ".cmake", "api", "v1", "reply");
                
                if (!Directory.Exists(replyDir))
                {
                    Log.LogMessage(MessageImportance.Low, $"CMake File API reply directory does not exist: {replyDir}");
                    Artifacts = Array.Empty<ITaskItem>();
                    return true;
                }

                // Find the latest index file
                var indexFiles = Directory.GetFiles(replyDir, "index-*.json");
                if (indexFiles.Length == 0)
                {
                    Log.LogMessage(MessageImportance.Low, "No CMake File API index files found.");
                    Artifacts = Array.Empty<ITaskItem>();
                    return true;
                }

                string indexFile = indexFiles.OrderByDescending(f => f).First();
                Log.LogMessage(MessageImportance.Low, $"Reading CMake File API index: {indexFile}");

                string indexJson = File.ReadAllText(indexFile);
                using var indexDoc = JsonDocument.Parse(indexJson);
                var root = indexDoc.RootElement;

                // Find codemodel in reply
                if (!root.TryGetProperty("reply", out var reply))
                {
                    Log.LogMessage(MessageImportance.Low, "No 'reply' property in index file.");
                    Artifacts = Array.Empty<ITaskItem>();
                    return true;
                }

                string codeModelFile = null;
                if (reply.TryGetProperty("codemodel-v2", out var codeModelRef))
                {
                    if (codeModelRef.TryGetProperty("jsonFile", out var jsonFile))
                    {
                        codeModelFile = Path.Combine(replyDir, jsonFile.GetString());
                    }
                }

                if (string.IsNullOrEmpty(codeModelFile) || !File.Exists(codeModelFile))
                {
                    Log.LogMessage(MessageImportance.Low, "Codemodel file not found in File API response.");
                    Artifacts = Array.Empty<ITaskItem>();
                    return true;
                }

                Log.LogMessage(MessageImportance.Low, $"Reading codemodel: {codeModelFile}");
                
                var artifacts = new List<ITaskItem>();
                
                string codeModelJson = File.ReadAllText(codeModelFile);
                using var codeModelDoc = JsonDocument.Parse(codeModelJson);
                var codeModel = codeModelDoc.RootElement;

                // Get the source root from the codemodel
                string sourceRoot = "";
                if (codeModel.TryGetProperty("paths", out var paths) && 
                    paths.TryGetProperty("source", out var sourceElement))
                {
                    sourceRoot = sourceElement.GetString().Replace('\\', '/').TrimEnd('/');
                }

                // Normalize source directory for comparison
                string normalizedSourceDir = Path.GetFullPath(SourceDirectory).Replace('\\', '/').TrimEnd('/');

                // Find the configuration
                if (codeModel.TryGetProperty("configurations", out var configurations))
                {
                    foreach (var config in configurations.EnumerateArray())
                    {
                        if (!config.TryGetProperty("name", out var configName) || 
                            !string.Equals(configName.GetString(), Configuration, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        Log.LogMessage(MessageImportance.Low, $"Found configuration: {Configuration}");

                        // Get targets array
                        if (!config.TryGetProperty("targets", out var targets))
                        {
                            continue;
                        }

                        var targetsList = new List<JsonElement>();
                        foreach (var target in targets.EnumerateArray())
                        {
                            targetsList.Add(target);
                        }

                        // Get directories
                        if (!config.TryGetProperty("directories", out var directories))
                        {
                            continue;
                        }

                        foreach (var directory in directories.EnumerateArray())
                        {
                            if (!directory.TryGetProperty("source", out var sourceDir))
                            {
                                continue;
                            }

                            string dirSource = sourceDir.GetString().Replace('\\', '/').TrimEnd('/');
                            
                            // Make the directory source path absolute
                            if (!Path.IsPathRooted(dirSource))
                            {
                                dirSource = Path.Combine(sourceRoot, dirSource);
                                dirSource = Path.GetFullPath(dirSource).Replace('\\', '/').TrimEnd('/');
                            }
                            
                            // Check if this directory matches the requested source directory
                            if (!string.Equals(dirSource, normalizedSourceDir, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            Log.LogMessage(MessageImportance.Low, $"Found matching directory: {dirSource}");

                            // Get targets in this directory
                            if (!directory.TryGetProperty("targetIndexes", out var targetIndexes))
                            {
                                continue;
                            }

                            foreach (var indexElement in targetIndexes.EnumerateArray())
                            {
                                int targetIndex = indexElement.GetInt32();
                                if (targetIndex < 0 || targetIndex >= targetsList.Count)
                                {
                                    continue;
                                }

                                var target = targetsList[targetIndex];
                                if (!target.TryGetProperty("jsonFile", out var targetJsonFile))
                                {
                                    continue;
                                }

                                string targetFile = Path.Combine(replyDir, targetJsonFile.GetString());
                                if (!File.Exists(targetFile))
                                {
                                    continue;
                                }

                                Log.LogMessage(MessageImportance.Low, $"Reading target file: {targetFile}");

                                // Read target details
                                string targetJson = File.ReadAllText(targetFile);
                                using var targetDoc = JsonDocument.Parse(targetJson);
                                var targetRoot = targetDoc.RootElement;

                                // Get artifacts
                                if (targetRoot.TryGetProperty("artifacts", out var artifactsArray))
                                {
                                    foreach (var artifact in artifactsArray.EnumerateArray())
                                    {
                                        if (artifact.TryGetProperty("path", out var artifactPath))
                                        {
                                            string fullPath = Path.Combine(CMakeOutputDir, artifactPath.GetString());
                                            fullPath = Path.GetFullPath(fullPath);
                                            
                                            var item = new TaskItem(fullPath);
                                            artifacts.Add(item);
                                            
                                            Log.LogMessage(MessageImportance.Low, $"Found artifact: {fullPath}");
                                        }
                                    }
                                }
                            }
                        }
                        
                        break; // Found the configuration, no need to continue
                    }
                }

                Artifacts = artifacts.ToArray();
                Log.LogMessage(MessageImportance.Normal, $"Found {Artifacts.Length} artifact(s) for source directory '{SourceDirectory}' in configuration '{Configuration}'");
                
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }
    }
}

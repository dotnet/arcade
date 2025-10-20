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
                    Log.LogError($"CMake File API reply directory does not exist: {replyDir}");
                    return false;
                }

                // Find the latest index file
                var indexFiles = Directory.GetFiles(replyDir, "index-*.json");
                if (indexFiles.Length == 0)
                {
                    Log.LogError("No CMake File API index files found.");
                    return false;
                }

                string indexFile = indexFiles.OrderByDescending(f => f).First();
                Log.LogMessage(MessageImportance.Low, $"Reading CMake File API index: {indexFile}");

                string indexJson = File.ReadAllText(indexFile);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var index = JsonSerializer.Deserialize<CMakeFileApiIndex>(indexJson, options);

                if (index?.Reply?.ClientReply?.CodemodelV2?.JsonFile == null)
                {
                    Log.LogError("No 'codemodel-v2' found in File API index reply.");
                    return false;
                }

                string codeModelFile = Path.Combine(replyDir, index.Reply.ClientReply.CodemodelV2.JsonFile);
                if (!File.Exists(codeModelFile))
                {
                    Log.LogError($"Codemodel file not found: {codeModelFile}");
                    return false;
                }

                Log.LogMessage(MessageImportance.Low, $"Reading codemodel: {codeModelFile}");
                
                string codeModelJson = File.ReadAllText(codeModelFile);
                var codeModel = JsonSerializer.Deserialize<CMakeCodeModel>(codeModelJson, options);

                if (codeModel == null)
                {
                    Log.LogError("Failed to deserialize codemodel.");
                    return false;
                }

                // Get the source root from the codemodel
                string sourceRoot = codeModel.Paths?.Source?.Replace('\\', '/').TrimEnd('/') ?? "";

                // Normalize source directory for comparison
                string normalizedSourceDir = Path.GetFullPath(SourceDirectory).Replace('\\', '/').TrimEnd('/');

                // Find the configuration
                var artifacts = new List<ITaskItem>();
                bool configurationFound = false;
                bool directoryFound = false;

                if (codeModel.Configurations != null)
                {
                    foreach (var config in codeModel.Configurations)
                    {
                        if (!string.Equals(config.Name, Configuration, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        configurationFound = true;
                        Log.LogMessage(MessageImportance.Low, $"Found configuration: {Configuration}");

                        if (config.Directories == null || config.Targets == null)
                        {
                            continue;
                        }

                        foreach (var directory in config.Directories)
                        {
                            string dirSource = directory.Source?.Replace('\\', '/').TrimEnd('/') ?? "";
                            
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

                            directoryFound = true;
                            Log.LogMessage(MessageImportance.Low, $"Found matching directory: {dirSource}");

                            // Get targets in this directory
                            if (directory.TargetIndexes == null)
                            {
                                continue;
                            }

                            foreach (int targetIndex in directory.TargetIndexes)
                            {
                                if (targetIndex < 0 || targetIndex >= config.Targets.Count)
                                {
                                    continue;
                                }

                                var target = config.Targets[targetIndex];
                                if (string.IsNullOrEmpty(target.JsonFile))
                                {
                                    continue;
                                }

                                string targetFile = Path.Combine(replyDir, target.JsonFile);
                                if (!File.Exists(targetFile))
                                {
                                    continue;
                                }

                                Log.LogMessage(MessageImportance.Low, $"Reading target file: {targetFile}");

                                // Read target details
                                string targetJson = File.ReadAllText(targetFile);
                                var targetDetails = JsonSerializer.Deserialize<CMakeTargetDetails>(targetJson, options);

                                // Get artifacts
                                if (targetDetails?.Artifacts != null)
                                {
                                    foreach (var artifact in targetDetails.Artifacts)
                                    {
                                        if (!string.IsNullOrEmpty(artifact.Path))
                                        {
                                            string fullPath = Path.Combine(CMakeOutputDir, artifact.Path);
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

                if (!configurationFound)
                {
                    Log.LogError($"Configuration '{Configuration}' not found in CMake File API response.");
                    return false;
                }

                if (!directoryFound)
                {
                    Log.LogError($"Source directory '{SourceDirectory}' not found in CMake File API response.");
                    return false;
                }

                if (artifacts.Count == 0)
                {
                    Log.LogWarning($"No artifacts found for source directory '{SourceDirectory}' in configuration '{Configuration}'.");
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

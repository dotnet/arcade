// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Arcade.Sdk.SourceBuild
{
    /// <summary>
    /// Reads entries in a Version.Details.xml file to find intermediate nupkg dependencies. For
    /// each dependency with a "SourceBuild" element, adds an item to the "Dependencies" output.
    /// </summary>
    public class ReadSourceBuildIntermediateNupkgDependencies : Task
    {
        [Required]
        public string VersionDetailsXmlFile { get; set; }

        [Required]
        public string SourceBuildIntermediateNupkgPrefix { get; set; }

        /// <summary>
        /// The intermediate nupkg RID to use if any RID-specific intermediate nupkgs are required.
        /// If this parameter isn't specified, RID-specific intermediate nupkgs can't be used and
        /// this task fails.
        /// </summary>
        public string SourceBuildIntermediateNupkgRid { get; set; }

        /// <summary>
        /// %(Identity): NuGet package ID.
        /// %(ExactVersion): NuGet package version. This can be used to look up the restored package
        ///   contents in a package cache.
        /// %(Version): NuGet package version, wrapped in "[version]" syntax for exact match.
        /// </summary>
        [Output]
        public ITaskItem[] Dependencies { get; set; }

        public override bool Execute()
        {
            XElement root = XElement.Load(VersionDetailsXmlFile, LoadOptions.PreserveWhitespace);

            XName CreateQualifiedName(string plainName)
            {
                return root.GetDefaultNamespace().GetName(plainName);
            }

            Dependencies = root
                .Elements()
                .Elements(CreateQualifiedName("Dependency"))
                .Select(d =>
                {
                    XElement sourceBuildElement = d.Element(CreateQualifiedName("SourceBuild"));

                    if (sourceBuildElement == null)
                    {
                        // Ignore element: doesn't represent a source-build dependency.
                        return null;
                    }

                    string repoName = sourceBuildElement.Attribute("RepoName")?.Value;

                    if (string.IsNullOrEmpty(repoName))
                    {
                        Log.LogError($"Dependency SourceBuild RepoName null or empty in '{VersionDetailsXmlFile}' element {d}");
                        return null;
                    }

                    string dependencyName = d.Attribute("Name")?.Value ?? string.Empty;

                    if (string.IsNullOrEmpty(dependencyName))
                    {
                        // Log name missing as FYI, but this is not an error case for source-build.
                        Log.LogMessage($"Dependency Name null or empty in '{VersionDetailsXmlFile}' element {d}");
                    }

                    string dependencyVersion = d.Attribute("Version")?.Value;

                    if (string.IsNullOrEmpty(dependencyVersion))
                    {
                        // We need a version to bring down an intermediate nupkg. Fail.
                        Log.LogError($"Dependency Version null or empty in '{VersionDetailsXmlFile}' element {d}");
                        return null;
                    }

                    string identity = SourceBuildIntermediateNupkgPrefix + repoName;

                    bool.TryParse(
                        sourceBuildElement.Attribute("ManagedOnly")?.Value,
                        out bool managedOnly);

                    // If RID-specific, add the RID to the end of the identity.
                    if (!managedOnly)
                    {
                        if (string.IsNullOrEmpty(SourceBuildIntermediateNupkgRid))
                        {
                            Log.LogError(
                                $"Parameter {nameof(SourceBuildIntermediateNupkgRid)} was " +
                                "not specified, indicating this project depends only on managed " +
                                "inputs. However, source-build element is not ManagedOnly: " +
                                sourceBuildElement);
                            return null;
                        }

                        identity += "." + SourceBuildIntermediateNupkgRid;
                    }

                    return new TaskItem(
                        identity,
                        new Dictionary<string, string>
                        {
                            ["Version"] = $"[{dependencyVersion}]",
                            ["ExactVersion"] = dependencyVersion
                        });
                })
                .Where(d => d != null)
                .ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}

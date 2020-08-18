// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Arcade.Sdk.SourceBuild
{
    /// <summary>
    /// Reads entries in a Version.Details.xml file to find intermediate nupkg
    /// dependencies. For each dependency with a "SourceBuildRepoName" element,
    /// adds an item to the "Dependencies" output.
    /// </summary>
    public class ReadSourceBuildIntermediateNupkgDependencies : Task
    {
        [Required]
        public string VersionDetailsXmlFile { get; set; }

        /// <summary>
        /// %(Identity): Value of the SourceBuildRepoName element of the dependency element.
        /// %(Name): Name attribute of the dependency element. Informational.
        /// %(Version): Version attribute of the dependency element.
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
                    string sourceBuildRepoName = d.Element(CreateQualifiedName("SourceBuildRepoName"))?.Value;

                    if (string.IsNullOrEmpty(sourceBuildRepoName))
                    {
                        // Ignore element: doesn't represent a source-build dependency.
                        return null;
                    }

                    string name = d.Attribute("Name")?.Value ?? string.Empty;

                    if (string.IsNullOrEmpty(name))
                    {
                        // Log name missing as FYI, but this is not an error case for source-build.
                        Log.LogMessage($"Dependency Name null or empty in '{VersionDetailsXmlFile}' element {d}");
                    }

                    string version = d.Attribute("Version")?.Value;

                    if (string.IsNullOrEmpty(version))
                    {
                        // We need a version to bring down an intermediate nupkg. Fail.
                        Log.LogError($"Dependency Version null or empty in '{VersionDetailsXmlFile}' element {d}");
                        return null;
                    }

                    return new TaskItem(
                        sourceBuildRepoName,
                        new Dictionary<string, string>
                        {
                            ["Name"] = name,
                            ["Version"] = version
                        });
                })
                .Where(d => d != null)
                .ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}

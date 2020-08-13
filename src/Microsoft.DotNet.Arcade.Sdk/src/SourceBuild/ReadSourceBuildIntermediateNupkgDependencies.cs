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
        /// %(Identity): Name attribute of the dependency element.
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
                .Where(d => d.Element(CreateQualifiedName("SourceBuildRepoName")) != null)
                .Select(d =>
                {
                    string name = d.Attribute("Name")?.Value;

                    if (string.IsNullOrEmpty(name))
                    {
                        Log.LogError($"Dependency Name null or empty in '{VersionDetailsXmlFile}' element {d}");
                        return null;
                    }

                    string version = d.Attribute("Version")?.Value;

                    if (string.IsNullOrEmpty(version))
                    {
                        Log.LogError($"Dependency Version null or empty in '{VersionDetailsXmlFile}' element {d}");
                        return null;
                    }

                    return new TaskItem(
                        name,
                        new Dictionary<string, string>
                        {
                            ["Version"] = version
                        });
                })
                .ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}

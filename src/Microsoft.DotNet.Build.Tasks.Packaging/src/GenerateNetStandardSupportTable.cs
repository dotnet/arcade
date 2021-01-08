// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GenerateNetStandardSupportTable : BuildTask
    {
        const string startMarker = "<!-- begin NetStandardSupportTable -->";
        const string endMarker = "<!-- end NetStandardSupportTable -->";

        [Required]
        public ITaskItem[] Reports
        {
            get;
            set;
        }

        [Required]
        public string DocFilePath
        {
            get;
            set;
        }

        public bool InsertIntoFile
        {
            get;
            set;
        }

        /// <summary>
        /// Generates a table in markdown that lists the API version supported by 
        /// various packages at all levels of NETStandard.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            if (Reports == null || Reports.Length == 0)
            {
                Log.LogError("Reports argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(DocFilePath))
            {
                Log.LogError("DocFilePath argument must be specified");
                return false;
            }

            string docDir = Path.GetDirectoryName(DocFilePath);
            if (!Directory.Exists(docDir))
            {
                Directory.CreateDirectory(docDir);
            }

            SortedSet<Version> knownNetStandardVersions = new SortedSet<Version>();
            List<SupportRow> rows = new List<SupportRow>(Reports.Length);

            foreach (var reportPath in Reports.Select(r => r.GetMetadata("FullPath")))
            {
                SupportRow row = new SupportRow();
                row.Name = Path.GetFileNameWithoutExtension(reportPath);
                row.SuportedVersions = new SortedSet<NETStandardApiVersion>();

                var report = PackageReport.Load(reportPath);

                foreach(var supportedFramework in report.SupportedFrameworks)
                {
                    var fx = NuGetFramework.Parse(supportedFramework.Key);

                    if (fx.Framework == FrameworkConstants.FrameworkIdentifiers.NetStandard)
                    {
                        row.SuportedVersions.Add(new NETStandardApiVersion(fx.Version, new Version(supportedFramework.Value.ToString())));
                        knownNetStandardVersions.Add(fx.Version);
                    }
                }
                rows.Add(row);
            }

            StringBuilder table = new StringBuilder();
            table.AppendLine($"| Contract | {String.Join(" | ", knownNetStandardVersions.Select(v => v.ToString(2)))} |");
            table.AppendLine($"| -------- | {String.Join(" | ", Enumerable.Repeat("---", knownNetStandardVersions.Count))}");

            foreach(var row in rows.OrderBy(r => r.Name))
            {
                if (row.SuportedVersions.Count == 0)
                {
                    Log.LogMessage($"Skipping {row.Name} since it has no supported NETStandard versions");
                    continue;
                }

                table.Append($"| {row.Name} |");
                    
                foreach (var netStandardVersion in knownNetStandardVersions)
                {
                    var apiVersion = row.SuportedVersions.LastOrDefault(a => a.NETStandardVersion <= netStandardVersion);

                    table.Append(" ");
                    if (apiVersion != null)
                    {
                        table.Append(apiVersion.APIVersion.ToString(3));
                    }
                    table.Append(" |");
                }
                table.AppendLine();
            }

            if (!InsertIntoFile)
            {
                File.WriteAllText(DocFilePath, table.ToString());
            }
            else
            {
                if (!File.Exists(DocFilePath))
                {
                    Log.LogError($"InsertIntoFile was specified as true but {DocFilePath} did not exist.");
                    return false;
                }

                string originalText = File.ReadAllText(DocFilePath);
                int startIndex = originalText.IndexOf(startMarker);

                if (startIndex < 0)
                {
                    Log.LogError($"InsertIntoFile was specified as true but could not locate insertion start text \"{startMarker}\".");
                    return false;
                }
                startIndex += startMarker.Length;
                // skip any white-space / new line
                while(startIndex < originalText.Length && Char.IsWhiteSpace(originalText[startIndex]))
                {
                    startIndex++;
                }

                int endIndex = originalText.IndexOf(endMarker, startIndex);

                if (endIndex < 0)
                {
                    Log.LogError($"InsertIntoFile was specified as true but could not locate insertion end text \"{endMarker}\".");
                    return false;
                }
                var docText = new StringBuilder(originalText);
                docText.Remove(startIndex, endIndex - startIndex);
                docText.Insert(startIndex, table.ToString());

                File.WriteAllText(DocFilePath, docText.ToString(), Encoding.UTF8);
            }


            return !Log.HasLoggedErrors;
        }

        private class SupportRow
        {
            public string Name { get; set; }
            public SortedSet<NETStandardApiVersion> SuportedVersions { get; set; }
        }

        private class NETStandardApiVersion : IComparable<NETStandardApiVersion>
        {
            public NETStandardApiVersion(Version netStandardVersion, Version apiVersion)
            {
                NETStandardVersion = netStandardVersion;
                APIVersion = apiVersion;
            }

            public Version NETStandardVersion { get; }
            public Version APIVersion {get;}

            public int CompareTo(NETStandardApiVersion other)
            {
                return NETStandardVersion.CompareTo(other.NETStandardVersion);
            }
        }
    }
}

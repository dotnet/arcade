// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class GetWorkloadPackPackageReferences : Microsoft.Build.Utilities.Task
    {
        public string ProjectFile
        {
            get;
            set;
        }

        public ITaskItem[] ExcludedPackIds
        {
            get;
            set;
        }

        public string PackageSource
        {
            get;
            set;
        }

        [Required]
        public ITaskItem[] ManifestFiles
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ProjectFile));

                List<string> packs = new();

                var excludedPackIds = ExcludedPackIds.Select(i => i.ItemSpec);

                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  "
                };

                XmlWriter writer = XmlWriter.Create(ProjectFile, settings);

                writer.WriteStartElement("Project");
                writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

                writer.WriteStartElement("PropertyGroup");
                writer.WriteElementString("TargetFramework", "net6.0");
                writer.WriteElementString("IncludeBuildOutput", "false");
                writer.WriteEndElement();

                writer.WriteStartElement("ItemGroup");

                foreach (var manifestFile in ManifestFiles)
                {
                    WorkloadManifest manifest = WorkloadManifestReader.ReadWorkloadManifest(Path.GetFileNameWithoutExtension(manifestFile.ItemSpec),
                        File.OpenRead(manifestFile.ItemSpec));

                    foreach (var pack in manifest.Packs.Values)
                    {
                        if (pack.IsAlias)
                        {
                            foreach (var alias in pack.AliasTo.Keys.Where(k => k.StartsWith("win")))
                            {
                                if (!excludedPackIds.Contains($"{pack.AliasTo[alias]}"))
                                {
                                    WriteItem(writer, "PackageDownload", ("Include", $"{pack.AliasTo[alias]}"), ("Version", $"[{pack.Version}]"));
                                    packs.Add($"$(NuGetPackageRoot){pack.AliasTo[alias]}\\{pack.Version}\\*.nupkg");
                                }
                            }
                        }
                        else if (!excludedPackIds.Contains($"{pack.Id}"))
                        {
                            WriteItem(writer, "PackageDownload", ("Include", $"{pack.Id}"), ("Version", $"[{pack.Version}]"));
                            packs.Add($"$(NuGetPackageRoot){pack.Id}\\{pack.Version}\\*.nupkg");
                        }
                    }
                }

                writer.WriteEndElement();

                writer.WriteStartElement("Target");
                writer.WriteAttributeString("Name", "CopyPacks");
                writer.WriteAttributeString("AfterTargets", "Build");

                writer.WriteStartElement("ItemGroup");

                foreach (var pack in packs)
                {
                    WriteItem(writer, "Pack", ("Include", pack));
                }                

                writer.WriteEndElement();

                writer.WriteStartElement("Copy");
                writer.WriteAttributeString("SourceFiles", "@(Pack)");
                writer.WriteAttributeString("DestinationFolder", $"{PackageSource}");
                writer.WriteEndElement();

                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.Flush();
                writer.Close();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private void WriteItem(XmlWriter writer, string itemName, params (string name, string value)[] metadata)
        {
            writer.WriteStartElement(itemName);

            foreach (var m in metadata)
            {
                writer.WriteAttributeString(m.name, m.value);
            }

            writer.WriteEndElement();
        }
    }
}

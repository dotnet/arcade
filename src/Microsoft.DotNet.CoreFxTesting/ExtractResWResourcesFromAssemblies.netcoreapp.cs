// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    public class ExtractResWResourcesFromAssemblies : Task
    {
        [Required]
        public ITaskItem[] InputAssemblies { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string InternalReswDirectory { get; set; }

        public override bool Execute()
        {
            try
            {
                Directory.CreateDirectory(OutputPath);
                CreateReswFiles();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
            }

            return !Log.HasLoggedErrors;
        }

        public void CreateReswFiles()
        {
            foreach (ITaskItem assemblySpec in InputAssemblies)
            {
                string assemblyPath = assemblySpec.ItemSpec;
                string assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);

                if (assemblyName.Equals("System.Private.CoreLib"))
                {
                    continue; // we don't want to extract the resources from Private CoreLib since this resources are managed by the legacy ResourceManager which gets them from the embedded and not from PRI. 
                }

                if (!ShouldExtractResources($"FxResources.{NormalizeAssemblyName(assemblyName)}.SR.resw", assemblyPath))
                {
                    continue; // we skip framework assemblies that resources already exist and don't need to be extracted to avoid reading dll metadata.
                }

                try
                {
                    using (FileStream assemblyStream = File.OpenRead(assemblyPath))
                    using (PEReader peReader = new PEReader(assemblyStream))
                    {
                        if (!peReader.HasMetadata)
                        {
                            continue; // native assembly
                        }

                        MetadataReader metadataReader = peReader.GetMetadataReader();
                        foreach (ManifestResourceHandle resourceHandle in metadataReader.ManifestResources)
                        {
                            ManifestResource resource = metadataReader.GetManifestResource(resourceHandle);

                            if (!resource.Implementation.IsNil)
                            {
                                continue; // not embedded resource
                            }

                            string resourceName = metadataReader.GetString(resource.Name);

                            if (!resourceName.EndsWith(".resources"))
                            {
                                continue; // we only need to get the resources strings to produce the resw files.
                            }

                            string reswName = $"{Path.GetFileNameWithoutExtension(resourceName)}.resw";

                            if (!reswName.StartsWith("FxResources") && !ShouldExtractResources(reswName, assemblyPath)) // already checked for FxResources previously
                            {
                                continue; // resw output file already exists and is up to date, so we should skip this resource file.
                            }

                            string reswPath = Path.Combine(OutputPath, reswName);
                            using (Stream resourceStream = GetResourceStream(peReader, resource))
                            using (ResourceReader resourceReader = new ResourceReader(resourceStream))
                            using (ReswResourceWriter resourceWriter = new ReswResourceWriter(reswPath))
                            {
                                IDictionaryEnumerator enumerator = resourceReader.GetEnumerator();
                                while (enumerator.MoveNext())
                                {
                                    resourceWriter.AddResource(enumerator.Key.ToString(), enumerator.Value.ToString());
                                }
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    continue; // not a Portable Executable.
                }
            }
        }

        // If the repo that we are building has some projects that contain an embedded resx file we will create the resw file for that project when building the src project in resources.targets
        // those resw files live in "InternalReswDirectory" so that is why in this case we don't need to check for a timestamp if the resw file already exists there we just skip those resources.
        // the reason why we skip it is because the incremental build will handle the timestamps, as we have a target to copy the resx files to resw files from EmbeddedResources inside the .csproj
        private bool ShouldExtractResources(string expectedReswFileName, string assemblyPath)
        {
            string internalReswPath = Path.Combine(InternalReswDirectory, expectedReswFileName);
            if (File.Exists(internalReswPath))
            {
                return false; // internal resw files are handled in build time by resources.targets, so we shouldn't care about timestamps since it uses incremental build
            }

            string externalReswPath = Path.Combine(OutputPath, expectedReswFileName);
            if (File.Exists(externalReswPath))
            {
                var reswFileInfo = new FileInfo(externalReswPath);
                var assemblyFileInfo = new FileInfo(assemblyPath);
                return reswFileInfo.LastWriteTimeUtc < assemblyFileInfo.LastWriteTimeUtc;
            }

            return true;
        }

        private unsafe Stream GetResourceStream(PEReader peReader, ManifestResource resource)
        {
            checked // arithmetic overflow here could cause AV
            {
                PEMemoryBlock memoryBlock = peReader.GetEntireImage();
                byte* peImageStart = memoryBlock.Pointer;
                byte* peImageEnd = peImageStart + memoryBlock.Length;

                // Locate resource's offset within the Portable Executable image.
                if (!peReader.PEHeaders.TryGetDirectoryOffset(peReader.PEHeaders.CorHeader.ResourcesDirectory, out int resourcesDirectoryOffset))
                {
                    throw new InvalidDataException("Failed to extract the resources from assembly when getting the offset to resources in the PE file.");
                }

                byte* resourceStart = peImageStart + resourcesDirectoryOffset + resource.Offset;

                // We need to get the resource length out from the first int in the resourceStart pointer
                if (resourceStart >= peImageEnd - sizeof(int))
                {
                    throw new InvalidDataException("Failed to extract the resources from assembly because resource offset was out of bounds.");
                }

                int resourceLength = new BlobReader(resourceStart, sizeof(int)).ReadInt32();
                resourceStart += sizeof(int);
                if (resourceLength < 0 || resourceStart >= peImageEnd - resourceLength)
                {
                    throw new InvalidDataException($"Failed to extract the resources from assembly because resource offset or length was out of bounds.");
                }

                return new UnmanagedMemoryStream(resourceStart, resourceLength);
            }
        }

        /// <summary>
        /// Normalize the passed name so it can be used as namespace name inside the code
        /// </summary>
        private static string NormalizeAssemblyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            bool insertUnderscore = char.IsNumber(name[0]);
            int i = 0;

            while (i < name.Length)
            {
                char c = name[i];
                if (!char.IsLetter(c) && c != '.' && c != '_' && !char.IsNumber(c))
                {
                    break;
                }

                i++;
            }

            if (i >= name.Length)
            {
                return insertUnderscore ? "_" + name : name;
            }

            var sb = new StringBuilder();
            if (insertUnderscore)
            {
                sb.Append('_');
            }

            for (int j = 0; j < i; j++)
            {
                sb.Append(name[j]);
            }

            sb.Append('_');
            i++;

            while (i < name.Length)
            {
                char c = name[i];
                if (char.IsLetter(c) || c == '.' || c == '_' || char.IsNumber(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }

                i++;
            }

            return sb.ToString();
        }

        private class ReswResourceWriter : IDisposable
        {
            private readonly XElement _root;
            private readonly XDocument _document;
            private readonly string _filePath;

            public ReswResourceWriter(string filePath)
            {
                _filePath = filePath;
                _document = XDocument.Parse(Headers);
                _root = _document.Element("root");
            }

            public void AddResource(string key, string value)
            {
                XNamespace ns = "http://www.w3.org/XML/1998/namespace";
                var newElement = new XElement("data",
                    new XAttribute("name", key),
                    new XAttribute(ns + "space", "preserve"),
                    new XElement("value", value));

                _root.Add(newElement);
            }

            public void Dispose()
            {
                using (Stream fileStream = File.Create(_filePath))
                {
                    _document.Save(fileStream);
                }
            }

            private const string Headers =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
                <root>
                <xsd:schema id=""root"" xmlns="""" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:msdata=""urn:schemas-microsoft-com:xml-msdata"">
                <xsd:import namespace=""http://www.w3.org/XML/1998/namespace"" />
                <xsd:element name=""root"" msdata:IsDataSet=""true"">
                    <xsd:complexType>
                    <xsd:choice maxOccurs=""unbounded"">
                        <xsd:element name=""metadata"">
                        <xsd:complexType>
                            <xsd:sequence>
                            <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" />
                            </xsd:sequence>
                            <xsd:attribute name=""name"" use=""required"" type=""xsd:string"" />
                            <xsd:attribute name=""type"" type=""xsd:string"" />
                            <xsd:attribute name=""mimetype"" type=""xsd:string"" />
                            <xsd:attribute ref=""xml:space"" />
                        </xsd:complexType>
                        </xsd:element>
                        <xsd:element name=""assembly"">
                        <xsd:complexType>
                            <xsd:attribute name=""alias"" type=""xsd:string"" />
                            <xsd:attribute name=""name"" type=""xsd:string"" />
                        </xsd:complexType>
                        </xsd:element>
                        <xsd:element name=""data"">
                        <xsd:complexType>
                            <xsd:sequence>
                            <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                            <xsd:element name=""comment"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""2"" />
                            </xsd:sequence>
                            <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" msdata:Ordinal=""1"" />
                            <xsd:attribute name=""type"" type=""xsd:string"" msdata:Ordinal=""3"" />
                            <xsd:attribute name=""mimetype"" type=""xsd:string"" msdata:Ordinal=""4"" />
                            <xsd:attribute ref=""xml:space"" />
                        </xsd:complexType>
                        </xsd:element>
                        <xsd:element name=""resheader"">
                        <xsd:complexType>
                            <xsd:sequence>
                            <xsd:element name=""value"" type=""xsd:string"" minOccurs=""0"" msdata:Ordinal=""1"" />
                            </xsd:sequence>
                            <xsd:attribute name=""name"" type=""xsd:string"" use=""required"" />
                        </xsd:complexType>
                        </xsd:element>
                    </xsd:choice>
                    </xsd:complexType>
                </xsd:element>
                </xsd:schema>
                <resheader name=""resmimetype"">
                <value>text/microsoft-resx</value>
                </resheader>
                <resheader name=""version"">
                <value>2.0</value>
                </resheader>
                <resheader name=""reader"">
                <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                </resheader>
                <resheader name=""writer"">
                <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
                </resheader>
                </root>";
        }
    }
}

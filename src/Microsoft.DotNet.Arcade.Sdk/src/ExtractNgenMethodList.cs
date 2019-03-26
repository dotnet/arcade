// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.Arcade.Sdk
{
    /// <summary>
    /// Used to convert a raw XML dump from IBCMerge into the set of methods which will be NGEN'd when 
    /// partial NGEN is enabled
    /// </summary>
    public sealed class ExtractNgenMethodList : Task
    {
        /// <summary>
        /// This is the XML file produced by passing -dxml to ibcmerge. It will be transformed into the set of
        /// methods marked for NGEN in the assembly
        /// </summary>
        [Required]
        public string IbcXmlFilePath { get; set; }

        /// <summary>
        /// This is the name of the assembly for when IBC is being run. 
        /// </summary>
        [Required]
        public string AssemblyFilePath { get; set; }

        /// <summary>
        /// The current target framework for the assembly being compiled.
        /// </summary>
        public string AssemblyTargetFramework { get; set; }

        /// <summary>
        /// This is the directory the NGEN list should be output to
        /// </summary>
        [Required]
        public string OutputDirectory { get; set; }

        public override bool Execute()
        {
            var document = XDocument.Load(IbcXmlFilePath);
            var items = new List<string>();
            var parentElement = document.Root.Element("MethodProfilingData");
            if (parentElement != null)
            {
                foreach (var child in parentElement.Elements("Item"))
                {
                    var flags = child.Attribute("flags");
                    var name = child.Attribute("name");
                    if (flags?.Value.Contains("ReadMethodCode") == true && name != null)
                    {
                        items.Add(name.Value);
                    }
                }
            }

            items.Sort();

            Directory.CreateDirectory(OutputDirectory);
            string outputFileName;
            if (GetAssemblyMvid(AssemblyFilePath, out Guid mvid))
            {
                outputFileName = $"{Path.GetFileNameWithoutExtension(AssemblyFilePath)}-{AssemblyTargetFramework}-{mvid}.ngen.txt";
            }
            else
            {
                Log.LogWarning($"Unable to read MVID and TargetFramework from {AssemblyFilePath}");
                outputFileName = $"{Guid.NewGuid()}.ngen.txt";
            }

            var outputFilePath = Path.Combine(OutputDirectory, outputFileName);
            using (var outputFileStream = new StreamWriter(outputFilePath, append: false))
            {
                foreach (var item in items)
                {
                    outputFileStream.WriteLine(item);
                }
            }

            return true;
        } 

        private static bool GetAssemblyMvid(string assemblyFilePath, out Guid mvid)
        {
            mvid = default;

            using (var stream = File.Open(assemblyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var pereader = new PEReader(stream))
            {
                if (pereader.HasMetadata)
                {
                    var metadataReader = pereader.GetMetadataReader();
                    var mvidHandle = metadataReader.GetModuleDefinition().Mvid;
                    mvid = metadataReader.GetGuid(mvidHandle);
                    return true;
                }
            }

            return false;
        }
    }
}

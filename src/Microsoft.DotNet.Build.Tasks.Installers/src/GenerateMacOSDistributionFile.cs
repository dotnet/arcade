// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.SharedFramework.Sdk
{
    public class GenerateMacOSDistributionFile : BuildTask
    {
        [Required]
        public string TemplatePath { get; set; }

        [Required]
        public string ProductBrandName { get; set; }

        [Required]
        public string TargetArchitecture { get; set; }

        [Required]
        public ITaskItem[] BundledPackages { get; set; }

        [Required]
        public string DestinationFile { get; set; }

        public override bool Execute()
        {
            try
            {
                XDocument document = XDocument.Load(TemplatePath);

                var titleElement = new XElement("title", $"{ProductBrandName} ({TargetArchitecture})");

                var choiceLineElements = BundledPackages.Select(component => new XElement("line", new XAttribute("choice", component.GetMetadata("FileNameWithExtension"))));

                var choiceElements = BundledPackages
                    .Select(component => new XElement("choice",
                        new XAttribute("id", component.GetMetadata("FileNameWithExtension")),
                        new XAttribute("visible", "true"),
                        new XAttribute("title", component.GetMetadata("Title")),
                        new XAttribute("description", component.GetMetadata("Description")),
                        new XElement("pkg-ref", new XAttribute("id", component.GetMetadata("FileNameWithExtension")))));

                var pkgRefElements = BundledPackages
                    .Select(component => new XElement("pkg-ref",
                        new XAttribute("id", component.GetMetadata("FileNameWithExtension")),
                        component.GetMetadata("FileNameWithExtension")));

                document.Root.Add(new XElement("choices-outline", choiceLineElements));
                document.Root.Add(choiceElements);
                document.Root.Add(pkgRefElements);
                using XmlWriter writer = XmlWriter.Create(File.OpenWrite(DestinationFile));
                document.WriteTo(writer);
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, false);
                return false;
            }
            return true;
        }
    }
}

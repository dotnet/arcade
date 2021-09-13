// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public string Alternativex64InstallPath { get; set; }

        public override bool Execute()
        {
            try
            {
                XDocument document = XDocument.Load(TemplatePath);

                var titleElement = new XElement("title", $"{ProductBrandName} ({TargetArchitecture})");

                IEnumerable<XElement> choiceLineElements;
                IEnumerable<XElement> choiceElements;
                XElement scriptElement = null;
                if (TargetArchitecture.Equals("x64"))
                {
                    Alternativex64InstallPath ??= "/usr/local/share/dotnet/x64";
                    var archScriptContent = @"<![CDATA[
function IsX64Machine() {
    var machine = system.sysctl(""hw.machine"");
    system.log(""Machine type: "" + machine);
    var result = machine == ""x64"" || machine.endsWith(""_x64"");
    system.log(""IsX64Machine: "" + result);
    return result;
}
]]>";
                    scriptElement = new XElement("script", new XText(archScriptContent));

                    choiceLineElements = BundledPackages.SelectMany(component => new XElement[] { new XElement("line", new XAttribute("choice", $"{component.GetMetadata("FileNameWithExtension")}.x64")),
                                                                                                  new XElement("line", new XAttribute("choice", $"{component.GetMetadata("FileNameWithExtension")}.arm64"))});

                    choiceElements = BundledPackages
                        .SelectMany(component => {
                            var visibleAttribute = new XAttribute("visible", "true");
                            var titleAttribute = new XAttribute("title", component.GetMetadata("Title"));
                            var descriptionAttribute = new XAttribute("description", component.GetMetadata("Description"));
                            var packageRefAttribute = new XElement("pkg-ref", new XAttribute("id", component.GetMetadata("FileNameWithExtension")));
                            var x64Element = new XElement("choice",
                                new XAttribute("id", $"{component.GetMetadata("FileNameWithExtension")}.x64"),
                                new XAttribute("selected", "IsX64Machine()"),
                                visibleAttribute,
                                titleAttribute,
                                descriptionAttribute,
                                packageRefAttribute);
                            var arm64Element = new XElement("choice",
                                new XAttribute("id", $"{component.GetMetadata("FileNameWithExtension")}.arm64"),
                                new XAttribute("selected", "!IsX64Machine()"),
                                new XAttribute("customLocation", Alternativex64InstallPath),
                                visibleAttribute,
                                titleAttribute,
                                descriptionAttribute,
                                packageRefAttribute);
                            
                            return new XElement[] { x64Element, arm64Element };
                        });
                }
                else {
                    choiceLineElements = BundledPackages.Select(component => new XElement("line", new XAttribute("choice", component.GetMetadata("FileNameWithExtension"))));

                    choiceElements = BundledPackages
                        .Select(component => new XElement("choice",
                            new XAttribute("id", component.GetMetadata("FileNameWithExtension")),
                            new XAttribute("visible", "true"),
                            new XAttribute("title", component.GetMetadata("Title")),
                            new XAttribute("description", component.GetMetadata("Description")),
                            new XElement("pkg-ref", new XAttribute("id", component.GetMetadata("FileNameWithExtension")))));
                }

                var pkgRefElements = BundledPackages
                    .Select(component => new XElement("pkg-ref",
                        new XAttribute("id", component.GetMetadata("FileNameWithExtension")),
                        component.GetMetadata("FileNameWithExtension")));
                        
                var optionsElement = document.Root.Element("options");
                bool templateHasOptions = optionsElement is not null;
                if (!templateHasOptions)
                {
                    optionsElement = new XElement("options");
                }
                if (optionsElement.Attribute("hostArchitectures") is null)
                {
                    string hostArchitecture = TargetArchitecture;
                    if (hostArchitecture == "x64")
                    {
                        hostArchitecture = "x86_64";
                    }
                    optionsElement.Add(new XAttribute("hostArchitectures", hostArchitecture));
                }
                
                if (!templateHasOptions)
                {
                    document.Root.Add(optionsElement);
                }

                document.Root.Add(titleElement);
                document.Root.Add(new XElement("choices-outline", choiceLineElements));
                document.Root.Add(choiceElements);
                document.Root.Add(pkgRefElements);
                if (scriptElement != null) {
                    document.Root.Add(scriptElement);
                }
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

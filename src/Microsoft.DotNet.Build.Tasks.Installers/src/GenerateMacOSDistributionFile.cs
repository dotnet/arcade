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

                var archScriptContent = @"<![CDATA[
function IsX64Machine() {
var machine = system.sysctl(""hw.machine"");
system.log(""Machine type: "" + machine);
var result = machine == ""x64"" || machine.endsWith(""_x64"");
system.log(""IsX64Machine: "" + result);
return result;
}
]]>";
                var scriptElement = new XElement("script", new XText(archScriptContent));

                var choiceElements = BundledPackages
                    .Select(component => new XElement("choice",
                        new XAttribute("id", component.GetMetadata("FileNameWithExtension")),
                        new XAttribute("visible", "true"),
                        new XAttribute("title", component.GetMetadata("Title")),
                        new XAttribute("description", component.GetMetadata("Description")),
                        new XElement("pkg-ref", new XAttribute("id", component.GetMetadata("FileNameWithExtension")))));

                if (TargetArchitecture == "x64")
                {
                    Alternativex64InstallPath ??= "/usr/local/share/dotnet/x64";

                    choiceElements =
                        choiceElements.Select(c => new XElement(c)
                            .WithAttribute("selected", "IsX64Machine()"))
                        .Concat(
                        choiceElements.Select(c => new XElement(c)
                            .WithAttribute("id", c.Attribute("id").Value + ".alternate")
                            .WithAttribute("selected", "!IsX64Machine()")
                            .WithAttribute("customLocation", Alternativex64InstallPath)));
                }

                var choiceLineElements = choiceElements
                    .Select(c => new XElement("line", new XAttribute("choice", c.Attribute("id").Value)));

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
                document.Root.Add(scriptElement);
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

    static class XElementExtensions
    {
        public static XElement WithAttribute(this XElement element, XName attribute, object value)
        {
            element.SetAttributeValue(attribute, value);
            return element;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Xml.Linq;

namespace XliffTasks
{
    /// <summary>
    /// A <see cref="TranslatableDocument"/> for files in CPS rule .xaml format
    /// See https://msdn.microsoft.com/en-us/library/ekyft91f(v=vs.100).aspx
    /// </summary>
    internal sealed class XamlRuleDocument : TranslatableXmlDocument
    {
        protected override IEnumerable<TranslatableNode> GetTranslatableNodes()
        {
            foreach (var element in Document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (XmlName(attribute) != "DisplayName" && XmlName(attribute) != "Description")
                    {
                        continue;
                    }

                    yield return new TranslatableXmlAttribute(
                        id: GenerateId(attribute),
                        source: attribute.Value,
                        note: "",
                        attribute: attribute);
                }
            }
        }

        private static string GenerateId(XAttribute attribute)
        {
            XElement parent = attribute.Parent;

            if (XmlName(parent) == "EnumValue")
            {
                XElement grandparent = parent.Parent;
                return $"{XmlName(parent)}|{AttributedName(grandparent)}.{AttributedName(parent)}|{XmlName(attribute)}";
            }

            return $"{XmlName(parent)}|{AttributedName(parent)}|{XmlName(attribute)}";
        }

        private static string XmlName(XElement element) => element.Name.LocalName;

        private static string XmlName(XAttribute attribute) => attribute.Name.LocalName;

        private static string AttributedName(XElement element) => element.Attribute("Name").Value;
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Xml.Linq;

namespace XliffTasks
{
    /// <summary>
    /// A <see cref="TranslatableDocument"/> for files in .resx format.
    /// See https://msdn.microsoft.com/en-us/library/ekyft91f(v=vs.100).aspx
    /// </summary>
    internal sealed class ResxDocument : TranslatableXmlDocument
    {
        protected override IEnumerable<TranslatableNode> GetTranslatableNodes()
        {
            foreach (var node in Document.Descendants("data"))
            {
                // skip non-string data
                if (node.Attribute("type") != null || node.Attribute("mimetype") != null)
                {
                    continue;
                }

                string name = node.Attribute("name").Value;
                XElement valueElement = node.Element("value");
                string value = valueElement?.Value;
                string comment = node.Element("comment")?.Value;

                // skip designer goo that should not be translated
                if (name.StartsWith(">>") || name.EndsWith(".LayoutSettings"))
                {
                    continue;
                }

                // skip fully "locked" strings
                if (comment == "{Locked}")
                {
                    continue;
                }

                // skip empty strings
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                yield return new TranslatableXmlElement(
                    id: name,
                    source: value, 
                    note: comment, 
                    element: valueElement);
            }
        }
    }
}

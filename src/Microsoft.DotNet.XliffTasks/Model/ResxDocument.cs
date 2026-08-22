// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace XliffTasks.Model
{
    /// <summary>
    /// A <see cref="TranslatableDocument"/> for files in .resx format.
    /// See https://msdn.microsoft.com/en-us/library/ekyft91f(v=vs.100).aspx
    /// </summary>
    internal sealed class ResxDocument : TranslatableXmlDocument
    {
        protected override IEnumerable<TranslatableNode> GetTranslatableNodes()
        {
            foreach (XElement node in Document.Descendants("data"))
            {
                // skip non-string data
                if (node.Attribute("type") != null || node.Attribute("mimetype") != null)
                {
                    continue;
                }

                string name = node.Attribute("name").Value;
                XElement valueElement = node.Element("value");
                string value = valueElement?.Value;
                string comment = node.Element("comment")?.Value ?? "";

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

        public override void RewriteRelativePathsForOutputPath(string sourceFullPath, string outputFullPath)
        {
            string sourceDir = Path.GetDirectoryName(sourceFullPath);
            string outputDir = Path.GetDirectoryName(outputFullPath);

            foreach (XElement node in Document.Descendants("data"))
            {
                if (node.Attribute("type")?.Value == "System.Resources.ResXFileRef, System.Windows.Forms")
                {
                    XElement valueNodeOfFileRef = node.Element("value");
                    string[] splitRelativePathAndSerializedType = valueNodeOfFileRef.Value.Split(';');
                    string resourceRelativePath = splitRelativePathAndSerializedType[0].Replace('\\', Path.DirectorySeparatorChar);

                    string absoluteResourcePath = Path.GetFullPath(Path.Combine(sourceDir, resourceRelativePath));
                    splitRelativePathAndSerializedType[0] = MakeRelativePath(outputDir, absoluteResourcePath);

                    valueNodeOfFileRef.Value = string.Join(";", splitRelativePathAndSerializedType);
                }
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

        public override void RewriteRelativePathsToAbsolute(string sourceFullPath)
        {
            foreach (XElement node in Document.Descendants("data"))
            {
                if (node.Attribute("type")?.Value == "System.Resources.ResXFileRef, System.Windows.Forms")
                {
                    XElement valueNodeOfFileRef = node.Element("value");
                    string[] splitRelativePathAndSerializedType = valueNodeOfFileRef.Value.Split(';');
                    string resourceRelativePath = splitRelativePathAndSerializedType[0].Replace('\\', Path.DirectorySeparatorChar);

                    string absolutePath = Path.Combine(Path.GetDirectoryName(sourceFullPath), resourceRelativePath);
                    splitRelativePathAndSerializedType[0] = absolutePath;

                    valueNodeOfFileRef.Value = string.Join(";", splitRelativePathAndSerializedType);
                }
            }
        }

        public override void RewriteRelativePathsForOutputPath(string sourceFullPath, string outputFullPath)
        {
            string sourceDirectory = Path.GetDirectoryName(sourceFullPath)
                ?? throw new ArgumentException($"Path '{sourceFullPath}' must include a directory.", nameof(sourceFullPath));
            string outputDirectory = Path.GetDirectoryName(outputFullPath)
                ?? throw new ArgumentException($"Path '{outputFullPath}' must include a directory.", nameof(outputFullPath));

            foreach (XElement node in Document.Descendants("data"))
            {
                if (node.Attribute("type")?.Value == "System.Resources.ResXFileRef, System.Windows.Forms")
                {
                    XElement valueNodeOfFileRef = node.Element("value");
                    string[] splitRelativePathAndSerializedType = valueNodeOfFileRef.Value.Split(';');
                    string resourceRelativePath = splitRelativePathAndSerializedType[0].Replace('\\', Path.DirectorySeparatorChar);
                    string absoluteResourcePath = Path.GetFullPath(Path.Combine(sourceDirectory, resourceRelativePath));

                    splitRelativePathAndSerializedType[0] = MakeRelativePath(outputDirectory, absoluteResourcePath);

                    valueNodeOfFileRef.Value = string.Join(";", splitRelativePathAndSerializedType);
                }
            }
        }

        private static string MakeRelativePath(string fromDirectory, string toPath)
        {
            string fromFullPath = Path.GetFullPath(fromDirectory);
            string toFullPath = Path.GetFullPath(toPath);
            string fromRoot = Path.GetPathRoot(fromFullPath);
            string toRoot = Path.GetPathRoot(toFullPath);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!string.Equals(fromRoot, toRoot, comparison))
            {
                return toFullPath;
            }

            Uri fromUri = new(AppendDirectorySeparator(fromFullPath));
            Uri toUri = new(toFullPath);
            string relativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString());
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            foreach (var node in Document.Descendants("data"))
            {
                if (node.Attribute("type")?.Value == "System.Resources.ResXFileRef, System.Windows.Forms")
                {
                    var valueNodeOfFileRef = node.Element("value");
                    var splitRelativePathAndSerializedType = valueNodeOfFileRef.Value.Split(';');
                    var resourceRelativePath = splitRelativePathAndSerializedType[0].Replace('\\', Path.DirectorySeparatorChar);

                    var absolutePath = Path.Combine(Path.GetDirectoryName(sourceFullPath), resourceRelativePath);
                    splitRelativePathAndSerializedType[0] = absolutePath;

                    valueNodeOfFileRef.Value = string.Join(";", splitRelativePathAndSerializedType);
                }
            }
        }

        /// <summary>
        /// Attempts to match formatting placeholders as documented at
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/composite-formatting.
        /// Explanation:
        /// \{
        ///    A placeholder starts with an open curly brace. Since curly braces are used in
        ///    regex syntax we escape it to be clear that we mean a literal {.
        ///
        /// (\d+)
        ///    The "index" component; one or more decimal digits. This is captured in a group
        ///    to facilitate extracting the numeric value.
        ///
        /// (\,\-?\d+)?
        ///    The optional "alignment" component. This is a comma, followed by an optional
        ///    minus sign, followed by one or more decimal digits.
        ///
        /// (\:[^\}]+)?
        ///    The optional "format string" componet. This is a colon, followed by one or more
        ///    characters that aren't close curly braces.
        ///
        /// \}
        ///    The close curly brace indicates the end of the placeholder.
        /// </summary>
        private static Regex s_placeHolderRegex = new Regex(@"\{(\d+)(\,\-?\d+)?(\:[^\}]+)?\}", RegexOptions.Compiled);

        /// <summary>
        /// Returns the number of replacement strings needed to properly format the given text.
        /// </summary>
        public static int GetReplacementCount(string text)
        {
            int placeHolderCount = 0;

            foreach (Match placeHolder in s_placeHolderRegex.Matches(text))
            {
                var index = int.Parse(placeHolder.Groups[1].Value);
                placeHolderCount = Math.Max(placeHolderCount, index + 1);
            }

            return placeHolderCount;
        }
    }
}

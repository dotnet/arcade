// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Fx.ApiReviews.Differencing.Exporters
{
    public sealed class MarkdownDiffExporter
    {
        private readonly DiffDocument _diffDocument;
        private readonly string _path;
        private readonly bool _includeTableOfContents;
        private readonly bool _createFilePerNamespace;

        public MarkdownDiffExporter(DiffDocument diffDocument, string path, bool includeTableOfContents, bool createFilePerNamespace)
        {
            _diffDocument = diffDocument;
            _path = path;
            _includeTableOfContents = includeTableOfContents;
            _createFilePerNamespace = createFilePerNamespace;
        }

        public void Export()
        {
            var noMasterFile = _createFilePerNamespace && !_includeTableOfContents;

            if (noMasterFile)
            {
                WriteDiffForNamespaces(null);
            }
            else
            {
                using (var writer = new StreamWriter(_path))
                {
                    WriteHeader(writer);

                    if (_includeTableOfContents)
                        WriteTableOfContents(writer);

                    WriteDiffForNamespaces(writer);
                }
            }
        }

        private void WriteHeader(StreamWriter writer)
        {
            if (_diffDocument.IsDiff)
            {
                writer.WriteLine("# API Difference {0} vs {1}", _diffDocument.Left.Name, _diffDocument.Right.Name);
            }
            else
            {
                var singleSideName = _diffDocument.Left.IsEmpty ? _diffDocument.Right.Name : _diffDocument.Left.Name;
                writer.WriteLine("# API List of {0}", singleSideName);
            }

            writer.WriteLine();

            if (_diffDocument.IsDiff)
            {
                writer.WriteLine("API listing follows standard diff formatting. Lines preceded by a '+' are");
                writer.WriteLine("additions and a '-' indicates removal.");
                writer.WriteLine();
            }
        }

        private void WriteTableOfContents(StreamWriter writer)
        {
            foreach (var topLevelApi in _diffDocument.ApiDefinitions)
            {
                var linkTitle = topLevelApi.Name;
                var linkTarget = GetLinkTarget(topLevelApi.Name);

                writer.WriteLine("* [{0}]({1})", linkTitle, linkTarget);
            }

            writer.WriteLine();
        }

        private void WriteDiffForNamespaces(StreamWriter writer)
        {
            if (_createFilePerNamespace)
            {
                foreach (var topLevelApi in _diffDocument.ApiDefinitions)
                {
                    var fileName = GetFileNameForNamespace(topLevelApi.Name);
                    using (var nestedWriter = new StreamWriter(fileName))
                        WriteDiffForNamespace(nestedWriter, topLevelApi, isStandalone: true);
                }
            }
            else
            {
                foreach (var topLevelApi in _diffDocument.ApiDefinitions)
                {
                    WriteDiffForNamespace(writer, topLevelApi, isStandalone: false);
                }
            }
        }

        private void WriteDiffForNamespace(StreamWriter writer, DiffApiDefinition topLevelApi, bool isStandalone)
        {
            var heading = _createFilePerNamespace ? "#" : "##";
            writer.WriteLine(heading + " " + topLevelApi.Name);
            writer.WriteLine();

            WriteDiff(writer, topLevelApi);

            writer.WriteLine();
        }

        private static void WriteDiff(StreamWriter writer, DiffApiDefinition topLevelApi)
        {
            writer.WriteLine("``` diff");
            WriteDiff(writer, topLevelApi, 0);
            writer.WriteLine("```");
        }

        private static void WriteDiff(StreamWriter writer, DiffApiDefinition api, int level)
        {
            var hasChildren = api.Children.Any();

            var indent = new string(' ', level * 4);
            var suffix = hasChildren ? " {" : string.Empty;
            var diff = api.Difference;

            if (diff == DifferenceType.Changed)
            {
                // Let's see whether the syntax actually changed. For some cases the syntax might not
                // diff, for example, when attribute declarations have changed.

                var left = api.Left.GetCSharpDeclaration();
                var right = api.Right.GetCSharpDeclaration();

                if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                    diff = DifferenceType.Unchanged;
            }

            if (diff == DifferenceType.Added)
            {
                WriteDiff(writer, "+", indent, suffix, api.Right);
            }
            else if (diff == DifferenceType.Changed || diff == DifferenceType.Removed)
            {
                WriteDiff(writer, "-", indent, suffix, api.Left);
                WriteDiff(writer, "+", indent, suffix, api.Right);
            }
            else
            {
                WriteDiff(writer, " ", indent, suffix, api.Definition);
            }

            if (hasChildren)
            {
                foreach (var child in api.Children)
                {
                    WriteDiff(writer, child, level + 1);
                }

                var diffMarker = diff == DifferenceType.Added
                                    ? "+"
                                    : diff == DifferenceType.Removed
                                        ? "-"
                                        : " ";

                writer.Write(diffMarker);
                writer.Write(indent);
                writer.WriteLine("}");
            }
        }

        private static void WriteDiff(TextWriter writer, string marker, string indent, string suffix, IDefinition api)
        {
            var lines = GetCSharpDecalarationLines(api);
            var isFirst = true;

            foreach (var line in lines)
            {
                if (isFirst)
                    isFirst = false;
                else
                    writer.WriteLine();

                writer.Write(marker);
                writer.Write(indent);
                writer.Write(line);
            }

            writer.WriteLine(suffix);
        }

        private static IEnumerable<string> GetCSharpDecalarationLines(IDefinition api)
        {
            var text = api.GetCSharpDeclaration();
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
        }

        private string GetLinkTarget(string namespaceName)
        {
            return _createFilePerNamespace
                        ? Path.GetFileName(GetFileNameForNamespace(namespaceName))
                        : "#" + GetAnchorName(namespaceName);
        }

        private string GetFileNameForNamespace(string namespaceName)
        {
            var directory = Path.GetDirectoryName(_path);
            var fileName = Path.GetFileNameWithoutExtension(_path);
            var extension = Path.GetExtension(_path);
            return Path.Combine(directory, fileName + "_" + namespaceName + extension);
        }

        private static string GetAnchorName(string name)
        {
            return name.Replace(".", "").ToLower();
        }
    }

}

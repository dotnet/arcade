// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace XliffTasks
{
    /// <summary>
    /// Represents a document in XLIFF format which can be updated from source from
    /// <see cref="TranslatableDocument"/> instances and produce translation data
    /// for <see cref="TranslatableDocument.Translate"/>.
    /// 
    /// See https://en.wikipedia.org/wiki/XLIFF
    /// </summary>
    internal sealed class XliffDocument : Document
    {
        private XDocument _document;

        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        public override bool HasContent => _document != null;

        /// <summary>
        /// Loads (or reloads) the document content from the given reader.
        /// </summary>
        public override void Load(TextReader reader)
        {
            _document = XDocument.Load(reader);
        }

        /// <summary>
        /// Loads initial document content for a new XLIFF document.
        /// </summary>
        public void LoadNew(string sourceDocumentId, string targetLanguage)
        {
            XNamespace ns = "urn:oasis:names:tc:xliff:document:1.2";
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

            _document = new XDocument(
                new XElement(ns + "xliff",
                    new XAttribute("xmlns", ns.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName),
                    new XAttribute("version", "1.2"),
                    new XAttribute(xsi + "schemaLocation", $"{ns.NamespaceName} xliff-core-1.2-transitional.xsd"),
                    new XElement(ns + "file",
                        new XAttribute("datatype", "xml"),
                        new XAttribute("source-language", "en"),
                        new XAttribute("target-language", targetLanguage),
                        new XAttribute("original", sourceDocumentId),
                        new XElement(ns + "body",
                            new XElement(ns + "group",
                                new XAttribute("id", sourceDocumentId))))));
        }

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given file path.
        /// </summary>
        public override void Save(TextWriter writer)
        {
            EnsureContent();
            _document.SaveCustom(writer);
        }

        /// <summary>
        /// Updates this XLIFF document with the source data from the given translatable document.
        /// </summary>
        /// <returns>True if any changes were made to this document.</returns>
        public bool Update(TranslatableDocument sourceDocument)
        {
            bool changed = false;
            Dictionary<string, TranslatableNode> nodesById = sourceDocument.Nodes.ToDictionary(n => n.Id);
            XNamespace ns = _document.Root.Name.Namespace;

            foreach (XElement unitElement in _document.Descendants(ns + "trans-unit").ToList())
            {
                XElement sourceElement = unitElement.Element(ns + "source");
                XElement noteElement = unitElement.Element(ns + "note");
                XAttribute idAttribute = unitElement.Attribute("id");
                XAttribute stateAttribute = unitElement.Element(ns + "target").Attribute("state");

                string id = idAttribute.Value;
                string state = stateAttribute.Value;
                string source = sourceElement.Value;
                string note = noteElement.Value;

                // delete node in document that has been removed from source 
                if (!nodesById.TryGetValue(id, out TranslatableNode sourceNode))
                {
                    unitElement.Remove();
                    changed = true;
                    continue;
                }

                // update trans-unit state if either the source text or associated note has change.
                if (source != sourceNode.Source || note != sourceNode.Note)
                {
                    sourceElement.Value = sourceNode.Source;
                    noteElement.Value = sourceNode.Note;
                    noteElement.SelfCloseIfPossible();

                    if (state == "translated")
                    {
                        stateAttribute.Value = "needs-review-translation";
                    }

                    changed = true;
                }

                // signal to loop below that this node is not new
                nodesById.Remove(id);
            }

            // Add new trans-units
            var bodyElement = _document.Descendants(ns + "body").Single();
            foreach (TranslatableNode sourceNode in sourceDocument.Nodes)
            {
                // Nodes that have been removed from nodesById table are not new and have already been handled.
                // Do not refactor this check away by iterating over dictionary values as the document order must be maintained deterministically.
                if (!nodesById.ContainsKey(sourceNode.Id))
                {
                    continue;
                }

                bodyElement.Add(
                    new XElement(ns + "trans-unit",
                        new XAttribute("id", sourceNode.Id),
                        new XElement(ns + "source", sourceNode.Source),
                        new XElement(ns + "target", new XAttribute("state", "new"), sourceNode.Source),
                        new XElement(ns + "note"), sourceNode.Note));

                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Gets the translations (key=id, value=target), which can
        /// be passed on to <see cref="TranslatableDocument.Translate"/>.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetTranslations()
        {
            var dictionary = new Dictionary<string, string>();
            var ns = _document.Root.Name.Namespace;

            foreach (var element in _document.Descendants(ns + "trans-unit"))
            {
                string id = element.Attribute("id").Value;
                string target = element.Attribute("target").Value;

                dictionary.Add(id, target);
            }

            return dictionary;
        }
    }
}
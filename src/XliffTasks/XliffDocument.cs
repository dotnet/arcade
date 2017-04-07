// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        public void Update(TranslatableDocument sourceDocument, string sourceDocumentName)
        {
            throw new NotImplementedException();
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
// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XliffTasks
{
    /// <summary>
    /// Represents a document that can be translated by applying substitutions to nodes.
    /// </summary>
    internal abstract class TranslatableDocument
    {
        /// <summary>
        /// The nodes of the document that can have substitutions.
        /// </summary>
        public IReadOnlyList<TranslatableNode> Nodes { get; private set; } = Array.Empty<TranslatableNode>();

        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        public bool HasContent { get; private set; }

        /// <summary>
        /// Loads (or reloads) the document from the given file path.
        /// </summary>
        public void Load(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Load(stream);
            }
        }

        /// <summary>
        /// Loads (or reloads) the document from the given stream.
        /// </summary>
        public void Load(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                Load(reader);
            }
        }

        /// <summary>
        /// Loads (or reloads) the document from the given stream.
        /// </summary>
        /// <param name="reader"></param>
        public void Load(TextReader reader)
        {
            LoadCore(reader);
            Nodes = GetTranslatableNodes().ToList().AsReadOnly();
            HasContent = true;
        }

        /// <summary>
        /// Applies the given translations to the document. 
        /// Keys align with <see cref="TranslatableNode.Id"/>.
        /// Values indicate the text with which to replace <see cref="TranslatableNode.Source"/>.
        /// </summary>
        public void Translate(IReadOnlyDictionary<string, string> translations)
        {
            EnsureLoaded();

            foreach (var node in Nodes)
            {
                if (translations.TryGetValue(node.Id, out string translation))
                {
                    node.Translate(translation);
                }
            }
        }

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given file path.
        /// </summary>
        public void Save(string path)
        {
            EnsureLoaded();

            using (var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                Save(stream);
            }
        }

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given stream.
        /// </summary>
        public void Save(Stream stream)
        {
            EnsureLoaded();

            using (var writer = new StreamWriter(stream))
            {
                Save(writer);
            }
        }

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given path.
        /// </summary>
        public void Save(TextWriter writer)
        {
            EnsureLoaded();

            SaveCore(writer);
        }

        protected abstract void LoadCore(TextReader reader);

        protected abstract void SaveCore(TextWriter writer);

        protected abstract IEnumerable<TranslatableNode> GetTranslatableNodes();

        private void EnsureLoaded()
        {
            if (!HasContent)
            {
                throw new InvalidOperationException("No content was loaded.");
            }
        }
    }
}

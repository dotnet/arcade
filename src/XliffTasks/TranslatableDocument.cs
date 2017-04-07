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
    internal abstract class TranslatableDocument : IDocument
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
        /// Loads (or reloads) the document content from the given reader.
        /// </summary>
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
            this.EnsureContent();

            foreach (var node in Nodes)
            {
                if (translations.TryGetValue(node.Id, out string translation))
                {
                    node.Translate(translation);
                }
            }
        }

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given writer.
        /// </summary>
        public void Save(TextWriter writer)
        {
            this.EnsureContent();

            SaveCore(writer);
        }

        protected abstract void LoadCore(TextReader reader);

        protected abstract void SaveCore(TextWriter writer);

        protected abstract IEnumerable<TranslatableNode> GetTranslatableNodes();
    }
}

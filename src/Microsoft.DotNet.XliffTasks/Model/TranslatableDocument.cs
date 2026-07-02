// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace XliffTasks.Model
{
    /// <summary>
    /// Represents a document that can be translated by applying substitutions to nodes.
    /// </summary>
    internal abstract class TranslatableDocument : Document
    {
        /// <summary>
        /// The nodes of the document that can have substitutions.
        /// </summary>
        public IReadOnlyList<TranslatableNode> Nodes { get; private set; } = Array.Empty<TranslatableNode>();

        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        public override bool HasContent => _hasContent;
        private bool _hasContent;

        /// <summary>
        /// Loads (or reloads) the document content from the given reader.
        /// </summary>
        public sealed override void Load(TextReader reader)
        {
            LoadCore(reader);
            Nodes = GetTranslatableNodes().ToList().AsReadOnly();
            _hasContent = true;
        }

        /// <summary>
        /// Saves the document's content to the given file path.
        /// </summary>
        public sealed override void Save(TextWriter writer)
        {
            EnsureContent();
            SaveCore(writer);
        }

        /// <summary>
        /// Applies the given translations to the document. 
        /// Keys align with <see cref="TranslatableNode.Id"/>.
        /// Values indicate the text with which to replace <see cref="TranslatableNode.Source"/>.
        /// </summary>
        public void Translate(IReadOnlyDictionary<string, string> translations)
        {
            EnsureContent();

            foreach (TranslatableNode node in Nodes)
            {
                if (translations.TryGetValue(node.Id, out string translation))
                {
                    node.Translate(translation);
                }
            }
       }

        // rewrite nodes that point to external files (used often for icons, etc.)
        // these will have relative paths adjusted to a path relative to the output file.
        public virtual void RewriteRelativePathsForOutputPath(string sourceFullPath, string outputFullPath)
        {
        }

        /// <summary>
        /// Computes a relative path from <paramref name="fromDirectory"/> to <paramref name="toPath"/>.
        /// Falls back to an absolute path if the paths are on different drives.
        /// </summary>
        protected static string MakeRelativePath(string fromDirectory, string toPath)
        {
            // Ensure fromDirectory ends with a separator so Uri treats it as a directory
            if (!fromDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !fromDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fromDirectory += Path.DirectorySeparatorChar;
            }

            Uri fromUri = new Uri(fromDirectory);
            Uri toUri = new Uri(toPath);

            // If on different drives (Windows), fall back to absolute path
            if (fromUri.Scheme != toUri.Scheme || fromUri.Host != toUri.Host)
            {
                return toPath;
            }

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }

        protected abstract void LoadCore(TextReader reader);

        protected abstract void SaveCore(TextWriter writer);

        protected abstract IEnumerable<TranslatableNode> GetTranslatableNodes();
    }
}

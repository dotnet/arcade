// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace XliffTasks
{
    internal abstract class Document
    {
        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        public abstract bool HasContent { get; }

        /// <summary>
        /// Loads (or reloads) the document content from the given file path.
        /// </summary>
        public void Load(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Load(stream);
            }
        }

        /// <summary>
        /// Loads (or reloads) the document content from the given stream.
        /// </summary>
        public void Load(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                Load(reader);
            }
        }

        /// <summary>
        /// Loads (or reloads) the document content from the given reader.
        /// </summary>
        public abstract void Load(TextReader reader);

        /// <summary>
        /// Saves the document's content to the given file path.
        /// </summary>
        public void Save(string path)
        {
            EnsureContent();

            using (var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                Save(stream);
            }
        }

        /// <summary>
        /// Saves the document's content to the given stream.
        /// </summary>
        public void Save(Stream stream)
        {
            EnsureContent();

            using (var writer = new StreamWriter(stream))
            {
                Save(writer);
            }
        }

        /// <summary>
        /// Saves the document's content to the given writer.
        /// </summary>
        public abstract void Save(TextWriter writer);

        /// <summary>
        /// Throws if this document has no content.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="HasContent"/> is false.</exception>
        protected void EnsureContent()
        {
            if (!HasContent)
            {
                throw new InvalidOperationException("Document has no content loaded.");
            }
        }
    }
}

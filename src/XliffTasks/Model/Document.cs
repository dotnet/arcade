// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

namespace XliffTasks.Model
{
    internal abstract class Document
    {
        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        public abstract bool HasContent { get; }

        private static Encoding s_utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        private static Encoding s_utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// The encoding to use when saving the document to a stream.
        /// This is the encoding that was detected on Load, or default value of UTF8-with-BOM for new documents.
        /// </summary>
        public Encoding Encoding { get; set; } = s_utf8WithBom;

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
            // NOTE: It is important to pass UTF8-without-BOM here and not UTF8-with-BOM (aka Encoding.UTF8 and also same
            // as default when not passing encoding). The reason is that CurrentEncoding is only different from the encoding 
            // provided when the StreamReader encounters a BOM. As such, if we start off with UTF8-with-BOM, we'll end up with 
            // UTF8-with-BOM even if the document has no BOM, which would defeat our purpose of preserving the encoding and BOM
            // of the original document in a Load/Modify/Save cycle.

            using (var reader = new StreamReader(stream, s_utf8WithoutBom, detectEncodingFromByteOrderMarks: true))
            {
                Load(reader);
                Encoding = reader.CurrentEncoding;
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

            using (var writer = new StreamWriter(stream, Encoding))
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

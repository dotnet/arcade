using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XliffTasks
{
    internal interface IDocument
    {
        /// <summary>
        /// Indicates if content has been loaded in to the document.
        /// </summary>
        bool HasContent { get; }

        /// <summary>
        /// Loads (or reloads) the document content from the given stream.
        /// </summary>
        void Load(TextReader reader);

        /// <summary>
        /// Saves the document's content (with translations applied if <see cref="Translate" /> was called) to the given path.
        /// </summary>
        void Save(TextWriter writer);
    }
}

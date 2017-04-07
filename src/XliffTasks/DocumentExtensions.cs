using System;
using System.IO;

namespace XliffTasks
{
    internal static class DocumentExtensions
    {
        /// <summary>
        /// Loads (or reloads) the document content from the given file path.
        /// </summary>
        public static void Load(this IDocument document, string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                document.Load(stream);
            }
        }

        /// <summary>
        /// Loads (or reloads) the document content from the given stream.
        /// </summary>
        public static void Load(this IDocument document, Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                document.Load(reader);
            }
        }

        /// <summary>
        /// Saves the document's content to the given file path.
        /// </summary>
        public static void Save(this IDocument document, string path)
        {
            document.EnsureContent();

            using (var stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                document.Save(stream);
            }
        }

        /// <summary>
        /// Saves the document's content to the given stream.
        /// </summary>
        public static void Save(this IDocument document, Stream stream)
        {
            document.EnsureContent();

            using (var writer = new StreamWriter(stream))
            {
                document.Save(writer);
            }
        }

        /// <summary>
        /// Throws if the given document has no content.
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="IDocument.HasContent"/> is false.</exception>
        public static void EnsureContent(this IDocument document)
        {
            if (!document.HasContent)
            {
                throw new InvalidOperationException("Document has no content loaded.");
            }
        }
    }
}

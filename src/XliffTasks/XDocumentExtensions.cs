using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace XliffTasks
{
    internal static class XDocumentExtensions
    {
        /// <summary>
        /// Save the given document to the given writer, with shared defaults
        /// for all XML writing by this library.
        /// </summary>
        public static void SaveCustom(this XDocument document, TextWriter writer)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = writer is StringWriter,
            };

            using (var xmlWriter = XmlWriter.Create(writer, settings))
            {
                document.Save(xmlWriter);
            }
        }
    }
}
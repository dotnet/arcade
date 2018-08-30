// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;
using System;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.DarcLib
{
    public class GitFile
    {
        public string FilePath { get; }

        public string Content { get; set; }

        public GitFile(string filePath, XmlDocument xmlDocument)
            : this(filePath, GetIndentedXmlBody(xmlDocument))
        {
        }

        public GitFile(string filePath, JObject jsonObject)
            : this(filePath, jsonObject.ToString(Newtonsoft.Json.Formatting.Indented))
        {
        }

        public GitFile(string filePath, string content)
        {
            FilePath = filePath;
            Content = content;
        }

        private static string GetIndentedXmlBody(XmlDocument xmlDocument)
        {
            XDocument doc = XDocument.Parse(xmlDocument.OuterXml);
            return $"{doc.Declaration}\n{doc.ToString()}";
        }
    }
}

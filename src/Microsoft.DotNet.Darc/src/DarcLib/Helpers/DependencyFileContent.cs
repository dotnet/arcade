// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json.Linq;
using System;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyFileContent
    {
        public string FilePath { get; }

        public string Content { get; set; }

        public DependencyFileContent(string filePath, XmlDocument xmlDocument)
            : this(filePath, GetIndentedXmlBody(xmlDocument))
        {
        }

        public DependencyFileContent(string filePath, JObject jsonObject)
            : this(filePath, jsonObject.ToString(Newtonsoft.Json.Formatting.Indented))
        {
        }

        public DependencyFileContent(string filePath, string content)
        {
            FilePath = filePath;
            Content = content;
        }

        public void Encode()
        {
            byte[] content = System.Text.Encoding.UTF8.GetBytes(Content);
            Content = Convert.ToBase64String(content);
        }

        public GitCommit ToCommit(string branch, string message = null)
        {
            message = message ?? $"Darc update of '{FilePath}'";
            Encode();
            GitCommit commit = new GitCommit(message, Content, branch);
            return commit;
        }

        private static string GetIndentedXmlBody(XmlDocument xmlDocument)
        {
            XDocument doc = XDocument.Parse(xmlDocument.OuterXml);
            return $"{doc.Declaration}\n{doc.ToString()}";
        }
    }
}

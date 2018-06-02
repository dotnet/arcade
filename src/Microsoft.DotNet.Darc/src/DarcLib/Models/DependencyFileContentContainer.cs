using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Microsoft.DotNet.Darc
{
    public class DependencyFileContentContainer
    {
        public DependencyFileContent VersionDetailsXml { get; set; }

        public DependencyFileContent VersionProps { get; set; }

        public DependencyFileContent GlobalJson { get; set; }
    }

    public class DependencyFileContent
    {
        public string FilePath { get; }

        public string Content { get; set; }

        public DependencyFileContent(string filePath, XmlDocument xmlDocument)
            : this(filePath, xmlDocument.OuterXml)
        {
        }

        public DependencyFileContent(string filePath, JObject jsonObject)
            : this(filePath, jsonObject.ToString())
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
    }
}

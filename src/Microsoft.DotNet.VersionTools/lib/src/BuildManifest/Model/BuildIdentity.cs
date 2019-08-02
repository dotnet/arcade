// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class BuildIdentity
    {
        private static readonly string[] AttributeOrder =
        {
            nameof(Name),
            nameof(BuildId),
            nameof(ProductVersion),
            nameof(Branch),
            nameof(Commit)
        };

        private static readonly string[] RequiredAttributes =
        {
            nameof(Name)
        };

        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Name
        {
            get { return Attributes.GetOrDefault(nameof(Name)); }
            set { Attributes[nameof(Name)] = value; }
        }

        public string BuildId
        {
            get { return Attributes.GetOrDefault(nameof(BuildId)); }
            set { Attributes[nameof(BuildId)] = value; }
        }

        public string ProductVersion
        {
            get { return Attributes.GetOrDefault(nameof(ProductVersion)); }
            set { Attributes[nameof(ProductVersion)] = value; }
        }

        public string Branch
        {
            get { return Attributes.GetOrDefault(nameof(Branch)); }
            set { Attributes[nameof(Branch)] = value; }
        }

        public string Commit
        {
            get { return Attributes.GetOrDefault(nameof(Commit)); }
            set { Attributes[nameof(Commit)] = value; }
        }

        public string IsStable
        {
            get { return Attributes.GetOrDefault(nameof(IsStable)); }
            set { Attributes[nameof(IsStable)] = value; }
        }

        public string PublishToFlatContainer
        {
            get { return Attributes.GetOrDefault(nameof(PublishToFlatContainer)); }
            set { Attributes[nameof(PublishToFlatContainer)] = value; }
        }

        public string VersionStamp
        {
            get { return Attributes.GetOrDefault(nameof(VersionStamp)); }
            set { Attributes[nameof(VersionStamp)] = value; }
        }

        public override string ToString()
        {
            string s = Name;
            if (!string.IsNullOrEmpty(ProductVersion))
            {
                s += $" {ProductVersion}";
            }
            if (!string.IsNullOrEmpty(Branch))
            {
                s += $" on '{Branch}'";
            }
            if (!string.IsNullOrEmpty(Commit))
            {
                s += $" ({Commit})";
            }
            if (!string.IsNullOrEmpty(BuildId))
            {
                s += $" build {BuildId}";
            }
            return s;
        }

        public IEnumerable<XAttribute> ToXmlAttributes() => Attributes
            .ThrowIfMissingAttributes(RequiredAttributes)
            .CreateXmlAttributes(AttributeOrder);

        public XElement ToXmlBuildElement() => new XElement("Build", ToXmlAttributes());

        public static BuildIdentity Parse(XElement xml) => new BuildIdentity
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes)
        };
    }
}

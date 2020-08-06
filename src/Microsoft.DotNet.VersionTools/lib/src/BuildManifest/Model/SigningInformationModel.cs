// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.VersionTools.BuildManifest.Model
{
    public class SigningInformationModel
    {
        public string AzureDevOpsCollectionUri
        {
            get { return Attributes.GetOrDefault(nameof(AzureDevOpsCollectionUri)); }
            set { Attributes[nameof(AzureDevOpsCollectionUri)] = value; }
        }

        public string AzureDevOpsProject
        {
            get { return Attributes.GetOrDefault(nameof(AzureDevOpsProject)); }
            set { Attributes[nameof(AzureDevOpsProject)] = value; }
        }

        public int AzureDevOpsBuildId
        {
            get { return int.Parse(Attributes.GetOrDefault(nameof(AzureDevOpsBuildId))); }
            set { Attributes[nameof(AzureDevOpsBuildId)] = value.ToString(); }
        }

        public List<FileExtensionSignInfoModel> FileExtensionSignInfo { get; set; } = new List<FileExtensionSignInfoModel>();

        public List<FileSignInfoModel> FileSignInfo { get; set; } = new List<FileSignInfoModel>();

        public List<ItemToSignModel> ItemsToSign { get; set; } = new List<ItemToSignModel>();

        public List<StrongNameSignInfoModel> StrongNameSignInfo { get; set; } = new List<StrongNameSignInfoModel>();

        public void Add(SigningInformationModel source)
        {
            FileExtensionSignInfo.AddRange(source.FileExtensionSignInfo);
            FileSignInfo.AddRange(source.FileSignInfo);
            ItemsToSign.AddRange(source.ItemsToSign);
            StrongNameSignInfo.AddRange(source.StrongNameSignInfo);
        }

        private static readonly string[] RequiredAttributes =
        {
            nameof(AzureDevOpsCollectionUri),
            nameof(AzureDevOpsProject),
            nameof(AzureDevOpsBuildId),
        };
        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        // The IsEmpty() check ensures that we dont' return a blank <SigningInformation> XElement
        // when there is no signing information, maintaining parity with the old asset manifest structure
        public XElement ToXml() => IsEmpty() ? null : new XElement(
            "SigningInformation",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(RequiredAttributes),
            Enumerable.Concat(
            FileExtensionSignInfo
                .OrderBy(fe => fe.Include, StringComparer.OrdinalIgnoreCase)
                .ThenBy(fe => fe.CertificateName, StringComparer.OrdinalIgnoreCase)
                .Select(fe => fe.ToXml()),
            FileSignInfo
                .OrderBy(f => f.Include, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.CertificateName, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.ToXml()))
            .Concat(ItemsToSign
                .OrderBy(i => i.Include, StringComparer.OrdinalIgnoreCase)
                .Select(i => i.ToXml()))
            .Concat(StrongNameSignInfo
                .OrderBy(s => s.Include, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.PublicKeyToken, StringComparer.OrdinalIgnoreCase)
                .Select(s => s.ToXml())));

        public bool IsEmpty() => !FileExtensionSignInfo.Any() && !FileSignInfo.Any()
            && !ItemsToSign.Any() && !StrongNameSignInfo.Any();

        public static SigningInformationModel Parse(XElement xml) => xml == null ? null : new SigningInformationModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes),
            FileExtensionSignInfo = xml.Elements("FileExtensionSignInfo").Select(FileExtensionSignInfoModel.Parse).ToList(),
            FileSignInfo = xml.Elements("FileSignInfo").Select(FileSignInfoModel.Parse).ToList(),
            ItemsToSign = xml.Elements("ItemsToSign").Select(ItemToSignModel.Parse).ToList(),
            StrongNameSignInfo = xml.Elements("StrongNameSignInfo").Select(StrongNameSignInfoModel.Parse).ToList(),
        };
    }

    public class FileExtensionSignInfoModel
    {
        private static readonly string[] RequiredAttributes =
        {
            nameof(Include),
            nameof(CertificateName)
        };
        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Include
        {
            get { return Attributes.GetOrDefault(nameof(Include)); }
            set { Attributes[nameof(Include)] = value; }
        }

        public string CertificateName
        {
            get { return Attributes.GetOrDefault(nameof(CertificateName)); }
            set { Attributes[nameof(CertificateName)] = value; }
        }
        public override string ToString() => $"Files \"*{Include}\" are signed with {CertificateName}";

        public XElement ToXml() => new XElement(
            "FileExtensionSignInfo",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(RequiredAttributes));

        public static FileExtensionSignInfoModel Parse(XElement xml) => new FileExtensionSignInfoModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes)
        };
    }

    public class FileSignInfoModel
    {
        private static readonly string[] RequiredAttributes =
        {
            nameof(Include),
            nameof(CertificateName)
        };
        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Include
        {
            get { return Attributes.GetOrDefault(nameof(Include)); }
            set { Attributes[nameof(Include)] = value; }
        }

        public string CertificateName
        {
            get { return Attributes.GetOrDefault(nameof(CertificateName)); }
            set { Attributes[nameof(CertificateName)] = value; }
        }
        public override string ToString() => $"File \"{Include}\" is signed with {CertificateName}";

        public XElement ToXml() => new XElement(
            "FileSignInfo",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(RequiredAttributes));

        public static FileSignInfoModel Parse(XElement xml) => new FileSignInfoModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes)
        };
    }

    public class ItemToSignModel
    {
        private static readonly string[] RequiredAttributes =
        {
            nameof(Include)
        };
        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Include
        {
            get { return Attributes.GetOrDefault(nameof(Include)); }
            set { Attributes[nameof(Include)] = value; }
        }
        public override string ToString() => $"Signed: {Include}";

        public XElement ToXml() => new XElement(
            "ItemsToSign",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(RequiredAttributes));

        public static ItemToSignModel Parse(XElement xml) => new ItemToSignModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes)
        };
    }

    public class StrongNameSignInfoModel
    {
        private static readonly string[] RequiredAttributes =
        {
            nameof(Include),
            nameof(PublicKeyToken),
            nameof(CertificateName)
        };
        public IDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public string Include
        {
            get { return Attributes.GetOrDefault(nameof(Include)); }
            set { Attributes[nameof(Include)] = value; }
        }
        public string PublicKeyToken
        {
            get { return Attributes.GetOrDefault(nameof(PublicKeyToken)); }
            set { Attributes[nameof(PublicKeyToken)] = value; }
        }
        public string CertificateName
        {
            get { return Attributes.GetOrDefault(nameof(CertificateName)); }
            set { Attributes[nameof(CertificateName)] = value; }
        }
        public override string ToString() => $"{Include} strong-name signed with certificate {CertificateName}, public key token: {PublicKeyToken}";

        public XElement ToXml() => new XElement(
            "StrongNameSignInfo",
            Attributes
                .ThrowIfMissingAttributes(RequiredAttributes)
                .CreateXmlAttributes(RequiredAttributes));

        public static StrongNameSignInfoModel Parse(XElement xml) => new StrongNameSignInfoModel
        {
            Attributes = xml
                .CreateAttributeDictionary()
                .ThrowIfMissingAttributes(RequiredAttributes)
        };
    }
}

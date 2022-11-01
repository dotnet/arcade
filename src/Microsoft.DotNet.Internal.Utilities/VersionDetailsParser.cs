// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.DotNet.Internal.Utilities
{

    public interface IVersionDetailsParser
    {
        IList<DependencyDetail> ParseVersionDetailsXml(string fileContents, bool includePinned = true);

        IList<DependencyDetail> ParseVersionDetailsXml(XmlDocument document, bool includePinned = true);
    }

    public class VersionDetailsParser : IVersionDetailsParser
    {
        // Version.Details.xml schema
        public const string VersionPropsVersionElementSuffix = "PackageVersion";
        public const string VersionPropsAlternateVersionElementSuffix = "Version";
        public const string ShaElementName = "Sha";
        public const string UriElementName = "Uri";
        public const string DependencyElementName = "Dependency";
        public const string DependenciesElementName = "Dependencies";
        public const string NameAttributeName = "Name";
        public const string VersionAttributeName = "Version";
        public const string CoherentParentAttributeName = "CoherentParentDependency";
        public const string ProductDependencyElementName = "ProductDependencies";
        public const string ToolsetDependencyElementName = "ToolsetDependencies";
        public const string PinnedAttributeName = "Pinned";
        public const string AddElement = "add";
        public const string ClearElement = "clear";
        public const string KeyAttributeName = "key";
        public const string ValueAttributeName = "value";
        public const string SourceBuildElementName = "SourceBuild";
        public const string SourceBuildOldElementName = "SourceBuildTarball";
        public const string RepoNameAttributeName = "RepoName";
        public const string ManagedOnlyAttributeName = "ManagedOnly";
        public const string TarballOnlyAttributeName = "TarballOnly";

        // Version.Details.xml default location
        public const string VersionDetailsXmlLocation = "eng/Version.Details.xml";

        public IList<DependencyDetail> ParseVersionDetailsXml(string fileContents, bool includePinned = true)
        {
            XmlDocument document = GetXmlDocument(fileContents);
            return ParseVersionDetailsXml(document, includePinned: includePinned);
        }

        public IList<DependencyDetail> ParseVersionDetailsXml(XmlDocument document, bool includePinned = true)
        {
            XmlNodeList? dependencyNodes = document?.DocumentElement?.SelectNodes($"//{DependencyElementName}");
            if (dependencyNodes == null)
            {
                throw new Exception($"There was an error while reading '{VersionDetailsXmlLocation}' and it came back empty. " +
                    $"Look for exceptions above.");
            }

            var dependencies = ParseDependencyDetails(dependencyNodes);
            return includePinned ? dependencies : dependencies.Where(d => !d.Pinned).ToList();
        }

        private static List<DependencyDetail> ParseDependencyDetails(XmlNodeList dependencies)
        {
            List<DependencyDetail> dependencyDetails = new List<DependencyDetail>();

            foreach (XmlNode dependency in dependencies)
            {
                if (dependency.NodeType == XmlNodeType.Comment || dependency.NodeType == XmlNodeType.Whitespace)
                {
                    continue;
                }

                if (dependency.ParentNode is null)
                {
                    throw new ParserException($"{DependencyElementName} elements cannot be top-level; " +
                        $"they must belong to a group such as {ProductDependencyElementName}");
                }

                if (dependency.Attributes is null)
                {
                    throw new ParserException($"Dependencies cannot be top-level and must belong to a group such as {ProductDependencyElementName}");
                }

                var type = dependency.ParentNode.Name switch
                {
                    ProductDependencyElementName => DependencyType.Product,
                    ToolsetDependencyElementName => DependencyType.Toolset,
                    _ => throw new ParserException($"Unknown dependency type '{dependency.ParentNode.Name}'"),
                };

                // If the 'Pinned' attribute does not exist or if it is set to false we just not update it
                bool isPinned = ParseBooleanAttribute(dependency.Attributes, PinnedAttributeName);

                XmlNode? sourceBuildNode = dependency.SelectSingleNode(SourceBuildElementName)
                    ?? dependency.SelectSingleNode(SourceBuildOldElementName); // Workaround for https://github.com/dotnet/source-build/issues/2481

                SourceBuildInfo? sourceBuildInfo = null;
                if (sourceBuildNode is XmlElement sourceBuildElement)
                {
                    string repoName = sourceBuildElement.Attributes[RepoNameAttributeName]?.Value
                        ?? throw new ParserException($"{RepoNameAttributeName} of {SourceBuildElementName} " +
                                                   $"null or empty in '{dependency.Attributes[NameAttributeName]?.Value}'");

                    sourceBuildInfo = new SourceBuildInfo
                    {
                        RepoName = repoName,
                        ManagedOnly = ParseBooleanAttribute(sourceBuildElement.Attributes, ManagedOnlyAttributeName),
                        TarballOnly = ParseBooleanAttribute(sourceBuildElement.Attributes, TarballOnlyAttributeName),
                    };
                }

                var dependencyDetail = new DependencyDetail
                {
                    Name = dependency.Attributes[NameAttributeName]?.Value?.Trim(),
                    RepoUri = dependency.SelectSingleNode(UriElementName)?.InnerText?.Trim(),
                    Commit = dependency.SelectSingleNode(ShaElementName)?.InnerText?.Trim(),
                    Version = dependency.Attributes[VersionAttributeName]?.Value?.Trim(),
                    CoherentParentDependencyName = dependency.Attributes[CoherentParentAttributeName]?.Value?.Trim(),
                    Pinned = isPinned,
                    Type = type,
                    SourceBuild = sourceBuildInfo,
                };

                dependencyDetails.Add(dependencyDetail);
            }

            return dependencyDetails;
        }

        private static bool ParseBooleanAttribute(XmlAttributeCollection attributes, string attributeName)
        {
            var result = false;
            XmlAttribute? attribute = attributes[attributeName];
            if (attribute is not null && !bool.TryParse(attribute.Value, out result))
            {
                throw new ParserException($"The '{attributeName}' attribute is set but the value " +
                    $"'{attribute.Value}' is not a valid boolean...");
            }

            return result;
        }

        private static XmlDocument GetXmlDocument(string fileContent)
        {
            var document = new XmlDocument
            {
                PreserveWhitespace = true
            };

            document.LoadXml(fileContent);

            return document;
        }
    }

    [Serializable]
    public class ParserException : Exception
    {
        public ParserException()
        {
        }

        protected ParserException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public ParserException(string message) : base(message)
        {
        }

        public ParserException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

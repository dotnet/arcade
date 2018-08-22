using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.SignTool
{
    internal class Configuration
    {
        private TaskLoggingHelper _log;

        private string[] _explicitSignList;

        /// <summary>
        /// The URL of the feed where the package will be published.
        /// </summary>
        private string _publishUri;

        /// <summary>
        /// This store content information for container files.
        /// Key is a full file path.
        /// </summary>
        private Dictionary<string, ZipData> _zipDataMap;

        /// <summary>
        /// Path to where container files will be extracted.
        /// </summary>
        private string _pathToContainerUnpackingDirectory;

        /// <summary>
        /// This enable the overriding of the default certificate for a given file+token+target_framework.
        /// It also contains a SignToolConstants.IgnoreFileCertificateSentinel flag in the certificate name in case the file does not need to be signed
        /// for that 
        /// </summary>
        private Dictionary<ExplicitCertificateKey, string> _explicitCertificates;

        /// <summary>
        /// Used to look for signing information when we have the PublicKeyToken of a file.
        /// </summary>
        private Dictionary<string, SignInfo> _defaultSignInfoForPublicKeyToken;

        /// <summary>
        /// A list of all of the binaries that MUST be signed.
        /// </summary>
        private List<FileSignInfo> _filesToSign = new List<FileSignInfo>();

        public Configuration(string tempDir, string[] explicitSignList, Dictionary<string, SignInfo> defaultSignInfoForPublicKeyToken, Dictionary<ExplicitCertificateKey, string> explicitCertificates, string publishUri, TaskLoggingHelper log)
        {
            Debug.Assert(tempDir != null);
            Debug.Assert(explicitSignList != null && !explicitSignList.Any(i => i == null));
            Debug.Assert(defaultSignInfoForPublicKeyToken != null);
            Debug.Assert(explicitCertificates != null);

            _pathToContainerUnpackingDirectory = Path.Combine(tempDir, "ZipArchiveUnpackingDirectory");
            _log = log;
            _publishUri = publishUri;
            _defaultSignInfoForPublicKeyToken = defaultSignInfoForPublicKeyToken;
            _explicitCertificates = explicitCertificates;
            _zipDataMap = new Dictionary<string, ZipData>();
            _explicitSignList = explicitSignList;
        }

        internal BatchSignInput GenerateListOfFiles()
        {
            foreach (var fileNameToSign in _explicitSignList)
            {
                TrackFile(fileNameToSign);
            }

            return new BatchSignInput(_filesToSign.ToImmutableList(), _zipDataMap.ToImmutableDictionary(), _publishUri);
        }

        private FileSignInfo TrackFile(string fileFullPath)
        {
            var fileSignInfo = ExtractSignInfo(fileFullPath);
            if (fileSignInfo.SignInfo.IsAlreadySigned)
            {
                _log.LogMessage($"Ignoring already signed file: {fileFullPath}");
            }
            else if (fileSignInfo.SignInfo.ShouldIgnore)
            {
                _log.LogMessage($"Ignoring signing for this file: {fileFullPath}");
            }
            else
            {
                if (FileSignInfo.IsZipContainer(fileFullPath))
                {
                    if (BuildZipData(fileSignInfo, out var zipData))
                    {
                        _zipDataMap[fileFullPath] = zipData;
                    }
                }

                _filesToSign.Add(fileSignInfo);
            }

            return fileSignInfo;
        }

        private FileSignInfo ExtractSignInfo(string fullPath)
        {
            if (FileSignInfo.IsPEFile(fullPath))
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    if (ContentUtil.IsAuthenticodeSigned(stream))
                    {
                        return new FileSignInfo(fullPath, SignInfo.AlreadySigned);
                    }
                }

                GetPEInfo(fullPath, out var isManaged, out var publicKeyToken, out var targetFramework);

                // Get the default sign info based on the PKT, if applicable:
                if (!isManaged || !_defaultSignInfoForPublicKeyToken.TryGetValue(publicKeyToken, out var signInfo))
                {
                    signInfo = new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2);
                }

                // Check if we have more specific sign info:
                var fileName = Path.GetFileName(fullPath);
                if (_explicitCertificates.TryGetValue(new ExplicitCertificateKey(fileName, publicKeyToken, targetFramework), out var overridingCertificate) ||
                    _explicitCertificates.TryGetValue(new ExplicitCertificateKey(fileName, publicKeyToken), out overridingCertificate) ||
                    _explicitCertificates.TryGetValue(new ExplicitCertificateKey(fileName), out overridingCertificate))
                {
                    // If has overriding info, is it for ignoring the file?
                    if (overridingCertificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel))
                    {
                        return new FileSignInfo(fullPath, SignInfo.Ignore);
                    }

                    signInfo = signInfo.WithCertificateName(overridingCertificate);
                }

                return new FileSignInfo(fullPath, signInfo);
            }

            if (FileSignInfo.IsZipContainer(fullPath))
            {
                return new FileSignInfo(fullPath, new SignInfo(FileSignInfo.IsNupkg(fullPath) ? SignToolConstants.Certificate_NuGet : SignToolConstants.Certificate_VsixSHA2));
            }

            _log.LogWarning($"Unidentified artifact type: {fullPath}");
            return new FileSignInfo(fullPath, SignInfo.Ignore);
        }

        private static void GetPEInfo(string fullPath, out bool isManaged, out string publicKeyToken, out string targetFramework)
        {
            AssemblyName assemblyName;
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(fullPath);
                isManaged = true;
            }
            catch
            {
                isManaged = false;
                publicKeyToken = string.Empty;
                targetFramework = string.Empty;
                return;
            }

            var pktBytes = assemblyName.GetPublicKeyToken();

            publicKeyToken = (pktBytes == null || pktBytes.Length == 0) ? string.Empty : string.Join("", pktBytes.Select(b => b.ToString("x2")));
            targetFramework = GetTargetFrameworkName(fullPath);
        }

        private static string GetTargetFrameworkName(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var pereader = new PEReader(stream))
            {
                if (pereader.HasMetadata)
                {
                    var metadataReader = pereader.GetMetadataReader();

                    var assemblyDef = metadataReader.GetAssemblyDefinition();
                    foreach (var attributeHandle in assemblyDef.GetCustomAttributes())
                    {
                        var attribute = metadataReader.GetCustomAttribute(attributeHandle);
                        if (QualifiedNameEquals(metadataReader, attribute, "System.Runtime.Versioning", "TargetFrameworkAttribute"))
                        {
                            return new FrameworkName(GetTargetFrameworkAttributeValue(metadataReader, attribute)).FullName;
                        }
                    }
                }
            }

            return null;
        }

        private static bool QualifiedNameEquals(MetadataReader reader, CustomAttribute attribute, string namespaceName, string typeName)
        {
            bool qualifiedNameEquals(StringHandle nameHandle, StringHandle namespaceHandle)
                => reader.StringComparer.Equals(nameHandle, typeName) && reader.StringComparer.Equals(namespaceHandle, namespaceName);

            var ctorHandle = attribute.Constructor;
            switch (ctorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    var container = reader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                    switch (container.Kind)
                    {
                        case HandleKind.TypeReference:
                            var containerRef = reader.GetTypeReference((TypeReferenceHandle)container);
                            return qualifiedNameEquals(containerRef.Name, containerRef.Namespace);

                        case HandleKind.TypeDefinition:
                            var containerDef = reader.GetTypeDefinition((TypeDefinitionHandle)container);
                            return qualifiedNameEquals(containerDef.Name, containerDef.Namespace);

                        default:
                            return false;
                    }

                case HandleKind.MethodDefinition:
                    var typeDef = reader.GetTypeDefinition(reader.GetMethodDefinition((MethodDefinitionHandle)ctorHandle).GetDeclaringType());
                    return qualifiedNameEquals(typeDef.Name, typeDef.Namespace);

                default:
                    return false;
            }
        }

        private sealed class DummyCustomAttributeTypeProvider : ICustomAttributeTypeProvider<object>
        {
            public static readonly DummyCustomAttributeTypeProvider Instance = new DummyCustomAttributeTypeProvider();
            public object GetPrimitiveType(PrimitiveTypeCode typeCode) => null;
            public object GetSystemType() => null;
            public object GetSZArrayType(object elementType) => null;
            public object GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => null;
            public object GetTypeFromSerializedName(string name) => null;
            public PrimitiveTypeCode GetUnderlyingEnumType(object type) => default;
            public bool IsSystemType(object type) => false;
        }

        private static string GetTargetFrameworkAttributeValue(MetadataReader reader, CustomAttribute attribute)
        {
            var value = attribute.DecodeValue(DummyCustomAttributeTypeProvider.Instance);
            return (value.FixedArguments.Length == 1) ? value.FixedArguments[0].Value as string : null;
        }

        /// <summary>
        /// Build up the <see cref="ZipData"/> instance for a given zip container. This will also report any consistency
        /// errors found when examining the zip archive.
        /// </summary>
        private bool BuildZipData(FileSignInfo zipFileSignInfo, out ZipData zipData)
        {
            Debug.Assert(zipFileSignInfo.IsZipContainer());

            Package package = null;

            try
            {
                package = Package.Open(zipFileSignInfo.FullPath, FileMode.Open, FileAccess.Read);
                var packageTempDir = Path.Combine(_pathToContainerUnpackingDirectory, Guid.NewGuid().ToString());
                var nestedParts = new List<ZipPart>();

                foreach (var part in package.GetParts())
                {
                    var relativePath = GetPartRelativeFileName(part);
                    var packagePartTempName = Path.Combine(packageTempDir, relativePath);
                    var packagePartTempDirectory = Path.GetDirectoryName(packagePartTempName);

                    if (!FileSignInfo.IsZipContainer(packagePartTempName) && !FileSignInfo.IsPEFile(packagePartTempName))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(packagePartTempDirectory);

                    using (var tempFileStream = File.OpenWrite(packagePartTempName))
                    {
                        part.GetStream().CopyTo(tempFileStream);
                        tempFileStream.Close();
                    }

                    var fileSignInfo = TrackFile(packagePartTempName);
                    nestedParts.Add(new ZipPart(relativePath, fileSignInfo, null));
                }

                zipData = new ZipData(zipFileSignInfo, nestedParts.ToImmutableList());

                return true;
            }
            catch (Exception e)
            {
                _log.LogErrorFromException(e);
                zipData = null;
                return false;
            }
            finally
            {
                package?.Close();
            }
        }

        private static string GetPartRelativeFileName(PackagePart part)
        {
            var path = part.Uri.OriginalString;
            if (!string.IsNullOrEmpty(path) && path[0] == '/')
            {
                path = path.Substring(1);
            }

            return path;
        }
    }
}

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
using System.Security.Cryptography;

namespace Microsoft.DotNet.SignTool
{
    internal class Configuration
    {
        private readonly TaskLoggingHelper _log;

        private readonly string[] _explicitSignList;

        /// <summary>
        /// This store content information for container files.
        /// Key is the content hash of the file.
        /// </summary>
        private readonly Dictionary<ImmutableArray<byte>, ZipData> _zipDataMap;

        /// <summary>
        /// Path to where container files will be extracted.
        /// </summary>
        private readonly string _pathToContainerUnpackingDirectory;

        /// <summary>
        /// This enable the overriding of the default certificate for a given file+token+target_framework.
        /// It also contains a SignToolConstants.IgnoreFileCertificateSentinel flag in the certificate name in case the file does not need to be signed
        /// for that 
        /// </summary>
        private readonly Dictionary<ExplicitCertificateKey, string> _explicitCertificates;

        /// <summary>
        /// Used to look for signing information when we have the PublicKeyToken of a file.
        /// </summary>
        private readonly Dictionary<string, SignInfo> _defaultSignInfoForPublicKeyToken;

        /// <summary>
        /// A list of all of the binaries that MUST be signed.
        /// </summary>
        private readonly List<FileSignInfo> _filesToSign;

        private readonly Dictionary<ImmutableArray<byte>, FileSignInfo> _filesByContentHash;

        public Configuration(string tempDir, string[] explicitSignList, Dictionary<string, SignInfo> defaultSignInfoForPublicKeyToken, Dictionary<ExplicitCertificateKey, string> explicitCertificates, TaskLoggingHelper log)
        {
            Debug.Assert(tempDir != null);
            Debug.Assert(explicitSignList != null && !explicitSignList.Any(i => i == null));
            Debug.Assert(defaultSignInfoForPublicKeyToken != null);
            Debug.Assert(explicitCertificates != null);

            _pathToContainerUnpackingDirectory = Path.Combine(tempDir, "ContainerSigning");
            _log = log;
            _defaultSignInfoForPublicKeyToken = defaultSignInfoForPublicKeyToken;
            _explicitCertificates = explicitCertificates;
            _filesToSign = new List<FileSignInfo>();
            _zipDataMap = new Dictionary<ImmutableArray<byte>, ZipData>(ByteSequenceComparer.Instance);
            _filesByContentHash = new Dictionary<ImmutableArray<byte>, FileSignInfo>(ByteSequenceComparer.Instance);
            _explicitSignList = explicitSignList;
        }

        internal BatchSignInput GenerateListOfFiles()
        {
            foreach (var fullPath in _explicitSignList)
            {
                TrackFile(fullPath, ContentUtil.GetContentHash(fullPath));
            }

            return new BatchSignInput(_filesToSign.ToImmutableArray(), _zipDataMap.ToImmutableDictionary());
        }

        private FileSignInfo TrackFile(string fullPath, ImmutableArray<byte> contentHash)
        {
            var fileSignInfo = ExtractSignInfo(fullPath, contentHash);

            if (FileSignInfo.IsZipContainer(fullPath) && 
                !_zipDataMap.ContainsKey(fileSignInfo.ContentHash) && 
                TryBuildZipData(fileSignInfo, out var zipData))
            {
                _zipDataMap[fileSignInfo.ContentHash] = zipData;
            }
        
            if (fileSignInfo.SignInfo.ShouldSign)
            {
                _filesToSign.Add(fileSignInfo);
            }

            if (!_filesByContentHash.ContainsKey(contentHash))
            {
                _filesByContentHash.Add(contentHash, fileSignInfo);
            }

            return fileSignInfo;
        }

        private FileSignInfo ExtractSignInfo(string fullPath, ImmutableArray<byte> hash)
        {
            if (FileSignInfo.IsPEFile(fullPath))
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    if (ContentUtil.IsAuthenticodeSigned(stream))
                    {
                        return new FileSignInfo(fullPath, hash, SignInfo.AlreadySigned);
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
                    if (overridingCertificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel, StringComparison.OrdinalIgnoreCase))
                    {
                        return new FileSignInfo(fullPath, hash, SignInfo.Ignore);
                    }

                    signInfo = signInfo.WithCertificateName(overridingCertificate);
                }

                return new FileSignInfo(fullPath, hash, signInfo, (targetFramework != "") ? targetFramework : null);
            }

            if (FileSignInfo.IsZipContainer(fullPath))
            {
                // Use SignInfo.Ignore for zip files
                if (!FileSignInfo.IsZip(fullPath))
                {
                    return new FileSignInfo(fullPath, hash, new SignInfo(FileSignInfo.IsNupkg(fullPath) ? SignToolConstants.Certificate_NuGet : SignToolConstants.Certificate_VsixSHA2));
                }
            }
            else
            {
                _log.LogWarning($"Unidentified artifact type: {fullPath}");
            }

            return new FileSignInfo(fullPath, hash, SignInfo.Ignore);
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
        private bool TryBuildZipData(FileSignInfo zipFileSignInfo, out ZipData zipData)
        {
            Debug.Assert(zipFileSignInfo.IsZipContainer());

            Package package = null;

            try
            {
                package = Package.Open(zipFileSignInfo.FullPath, FileMode.Open, FileAccess.Read);
                var nestedParts = new List<ZipPart>();

                foreach (var part in package.GetParts())
                {
                    var relativePath = GetPartRelativeFileName(part);

                    if (!FileSignInfo.IsSignableFile(relativePath))
                    {
                        continue;
                    }

                    var stream = part.GetStream();

                    // copy the stream content, as the stream it doesn't support seek:
                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);

                    memoryStream.Position = 0;
                    var contentHash = ContentUtil.GetContentHash(memoryStream);

                    // if we already encountered file that hash the same content we can reuse its signed version when repackaging the container.
                    if (!_filesByContentHash.TryGetValue(contentHash, out var fileSignInfo))
                    {
                        var tempDir = Path.Combine(_pathToContainerUnpackingDirectory, ContentUtil.HashToString(contentHash));
                        var tempPath = Path.Combine(tempDir, Path.GetFileName(relativePath));
                        Directory.CreateDirectory(tempDir);
                        using (var tempFileStream = File.OpenWrite(tempPath))
                        {
                            memoryStream.Position = 0;
                            memoryStream.CopyTo(tempFileStream);
                            tempFileStream.Close();
                        }

                        fileSignInfo = TrackFile(tempPath, contentHash);
                    }

                    if (fileSignInfo.SignInfo.ShouldSign)
                    {
                        nestedParts.Add(new ZipPart(relativePath, fileSignInfo));
                    }
                }

                zipData = new ZipData(zipFileSignInfo, nestedParts.ToImmutableArray());

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

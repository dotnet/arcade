// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal class Configuration
    {
        private readonly TaskLoggingHelper _log;

        private readonly string[] _itemsToSign;

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
        private readonly Dictionary<ExplicitCertificateKey, string> _fileSignInfo;

        /// <summary>
        /// Used to look for signing information when we have the PublicKeyToken of a file.
        /// </summary>
        private readonly Dictionary<string, SignInfo> _strongNameInfo;

        /// <summary>
        /// A list of all the binaries that MUST be signed. Also include containers that don't need 
        /// to be signed themselves but include files that must be signed.
        /// </summary>
        private readonly List<FileSignInfo> _filesToSign;

        /// <summary>
        /// Mapping of ".ext" to certificate. Files that have an extension on this map
        /// will be signed using the specified certificate.
        /// </summary>
        private readonly Dictionary<string, SignInfo> _fileExtensionSignInfo;

        private readonly Dictionary<SignedFileContentKey, FileSignInfo> _filesByContentKey;

        /// <summary>
        /// For each uniquely identified file keeps track of all containers where the file appeared.
        /// </summary>
        private readonly Dictionary<SignedFileContentKey, HashSet<string>> _whichPackagesTheFileIsIn;

        /// <summary>
        /// Keeps track of all files that produced a given error code.
        /// </summary>
        private readonly Dictionary<SigningToolErrorCode, HashSet<SignedFileContentKey>> _errors;

        /// <summary>
        /// This is a list of the friendly name of certificates that can be used to
        /// sign already signed binaries.
        /// </summary>
        private readonly string[] _dualCertificates;

        /// <summary>
        /// Use the content hash in the path of the extracted file paths. 
        /// The default is to use a unique content id based on the number of items extracted.
        /// </summary>
        private readonly bool _useHashInExtractionPath; 
        
        /// <summary>
        /// A list of files whose content needs to be overwritten by signed content from a different file.
        /// Copy the content of file with full path specified in Key to file with full path specified in Value.
        /// </summary>
        internal List<KeyValuePair<string, string>> _filesToCopy;

        public Configuration(string tempDir, string[] itemsToSign, Dictionary<string, SignInfo> strongNameInfo,
            Dictionary<ExplicitCertificateKey, string> fileSignInfo, Dictionary<string, SignInfo> extensionSignInfo,
            string[] dualCertificates, TaskLoggingHelper log, bool useHashInExtractionPath = false)
        {
            Debug.Assert(tempDir != null);
            Debug.Assert(itemsToSign != null && !itemsToSign.Any(i => i == null));
            Debug.Assert(strongNameInfo != null);
            Debug.Assert(fileSignInfo != null);

            _pathToContainerUnpackingDirectory = Path.Combine(tempDir, "ContainerSigning");
            _useHashInExtractionPath = useHashInExtractionPath;
            _log = log;
            _strongNameInfo = strongNameInfo;
            _fileSignInfo = fileSignInfo;
            _fileExtensionSignInfo = extensionSignInfo;
            _filesToSign = new List<FileSignInfo>();
            _filesToCopy = new List<KeyValuePair<string, string>>();
            _zipDataMap = new Dictionary<ImmutableArray<byte>, ZipData>(ByteSequenceComparer.Instance);
            _filesByContentKey = new Dictionary<SignedFileContentKey, FileSignInfo>();
            _itemsToSign = itemsToSign;
            _dualCertificates = dualCertificates ?? new string[0];
            _whichPackagesTheFileIsIn = new Dictionary<SignedFileContentKey, HashSet<string>>();
            _errors = new Dictionary<SigningToolErrorCode, HashSet<SignedFileContentKey>>();
        }

        internal void ReadExistingContainerSigningCache()
        {
            _log.LogMessage("Loading existing files from cache");
            foreach (var file in Directory.EnumerateFiles(_pathToContainerUnpackingDirectory, "*.*", SearchOption.AllDirectories))
            {
                string cacheRelative = file.Replace(_pathToContainerUnpackingDirectory + Path.DirectorySeparatorChar, "");
                int indexOfHash = cacheRelative.IndexOf(Path.DirectorySeparatorChar);

                if (indexOfHash <= 0)
                {
                    continue;
                }

                // When reading from an existing cache use the already computed hash from the directory
                // structure instead of computing it from the file because things like signing 
                // might have changed the hash but we want to still use the same hash of the unsigned
                // file that originally built the cache. 
                string stringHash = cacheRelative.Substring(0, indexOfHash);
                ImmutableArray<byte> contentHash;
                try
                {
                    contentHash = ContentUtil.StringToHash(stringHash);
                }
                catch
                {
                    _log.LogMessage($"Failed to parse the content hash from path '{file}' so skipping it.");
                    continue;
                }

                // if the content of the file doesn't match the hash in file path than the file has changed
                // which indicates that it was signed so we need to ensure we repack the binary with the signed version
                string actualFileHash = ContentUtil.HashToString(ContentUtil.GetContentHash(file));
                bool forceRepack = stringHash != actualFileHash;

                TrackFile(file, contentHash, false, forceRepack);
            }
            _log.LogMessage("Done loading existing files from cache");
        }

        internal BatchSignInput GenerateListOfFiles()
        {
            foreach (var fullPath in _itemsToSign)
            {
                var fileUniqueKey = new SignedFileContentKey(ContentUtil.GetContentHash(fullPath), fullPath);

                if (!_whichPackagesTheFileIsIn.TryGetValue(fileUniqueKey, out var packages))
                {
                    packages = new HashSet<string>();
                }

                packages.Add(fullPath);
                _whichPackagesTheFileIsIn[fileUniqueKey] = packages;

                TrackFile(fullPath, ContentUtil.GetContentHash(fullPath), isNested: false);
            }

            if (_errors.Any())
            {
                // Iterate over each pair of <error code, unique file identity>. 
                // We can be sure here that the same file won't have the same error code twice.
                foreach (var errorGroup in _errors)
                {
                    switch (errorGroup.Key)
                    {
                        case SigningToolErrorCode.SIGN002:
                            _log.LogError("Could not determine certificate name for signable file(s):");
                            break;
                    }

                    // For each file that had that error
                    foreach (var erroedFile in errorGroup.Value)
                    {
                        _log.LogError($"\tFile: {erroedFile.FileName}");

                        // Get a list of all containers where the file showed up
                        foreach (var containerName in _whichPackagesTheFileIsIn[erroedFile])
                        {
                            _log.LogError($"\t\t{containerName}");
                        }
                    }
                }
            }

            return new BatchSignInput(_filesToSign.ToImmutableArray(), _zipDataMap.ToImmutableDictionary(ByteSequenceComparer.Instance), _filesToCopy.ToImmutableArray());
        }

        private FileSignInfo TrackFile(string fullPath, ImmutableArray<byte> contentHash, bool isNested, bool forceRepack = false)
        {
            _log.LogMessage($"Tracking file '{fullPath}' isNested={isNested}");
            var fileSignInfo = ExtractSignInfo(fullPath, contentHash, forceRepack);

            var key = new SignedFileContentKey(contentHash, Path.GetFileName(fullPath));

            if (_filesByContentKey.TryGetValue(key, out var existingSignInfo))
            {
                // If we saw this file already we wouldn't call TrackFile unless this is a top-level file.
                Debug.Assert(!isNested);

                // Copy the signed content to the destination path.
                _filesToCopy.Add(new KeyValuePair<string, string>(existingSignInfo.FullPath, fullPath));
                return fileSignInfo;
            }

            if (FileSignInfo.IsZipContainer(fullPath))
            {
                Debug.Assert(!_zipDataMap.ContainsKey(contentHash));

                if (TryBuildZipData(fileSignInfo, out var zipData))
                {
                    _zipDataMap[contentHash] = zipData;
                }
            }

            _log.LogMessage(MessageImportance.Low, $"Caching file {key.FileName} {key.StringHash}");
            _filesByContentKey.Add(key, fileSignInfo);

            if (fileSignInfo.SignInfo.ShouldSign || fileSignInfo.ForceRepack || fileSignInfo.IsZipContainer())
            {
                _filesToSign.Add(fileSignInfo);
            }

            return fileSignInfo;
        }

        private FileSignInfo ExtractSignInfo(string fullPath, ImmutableArray<byte> hash, bool forceRepack = false)
        {
            // Try to determine default certificate name by the extension of the file
            var hasSignInfo = _fileExtensionSignInfo.TryGetValue(Path.GetExtension(fullPath), out var signInfo);
            var fileName = Path.GetFileName(fullPath);
            var extension = Path.GetExtension(fullPath);
            string explicitCertificateName = null;
            var fileSpec = string.Empty;
            var isAlreadySigned = false;
            var matchedNameTokenFramework = false;
            var matchedNameToken = false;
            var matchedName = false;
            PEInfo peInfo = null;

            if (FileSignInfo.IsPEFile(fullPath))
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    isAlreadySigned = ContentUtil.IsAuthenticodeSigned(stream);
                }

                peInfo = GetPEInfo(fullPath);

                // Get the default sign info based on the PKT, if applicable:
                if (peInfo.IsManaged && _strongNameInfo.TryGetValue(peInfo.PublicKeyToken, out var pktBasedSignInfo))
                {
                    if (peInfo.IsCrossgened)
                    {
                        signInfo = new SignInfo(pktBasedSignInfo.Certificate);
                    }
                    else
                    {
                        signInfo = pktBasedSignInfo;
                    }
                    hasSignInfo = true;

                }

                // Check if we have more specific sign info:
                matchedNameTokenFramework = _fileSignInfo.TryGetValue(new ExplicitCertificateKey(fileName, peInfo.PublicKeyToken, peInfo.TargetFramework), out explicitCertificateName);
                matchedNameToken = !matchedNameTokenFramework && _fileSignInfo.TryGetValue(new ExplicitCertificateKey(fileName, peInfo.PublicKeyToken), out explicitCertificateName);

                fileSpec = matchedNameTokenFramework ? $" (PublicKeyToken = {peInfo.PublicKeyToken}, Framework = {peInfo.TargetFramework})" :
                        matchedNameToken ? $" (PublicKeyToken = {peInfo.PublicKeyToken})" : string.Empty;
            }

            // We didn't find any specific information for PE files using PKT + TargetFramework
            if (explicitCertificateName == null)
            {
                matchedName = _fileSignInfo.TryGetValue(new ExplicitCertificateKey(fileName), out explicitCertificateName);
            }

            // If has overriding info, is it for ignoring the file?
            if (SignToolConstants.IgnoreFileCertificateSentinel.Equals(explicitCertificateName, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogMessage($"File configured to not be signed: {fileName}{fileSpec}");
                return new FileSignInfo(fullPath, hash, SignInfo.Ignore, forceRepack:forceRepack);
            }

            // Do we have an explicit certificate after all?
            if (explicitCertificateName != null)
            {
                signInfo = signInfo.WithCertificateName(explicitCertificateName);
                hasSignInfo = true;
            }

            if (hasSignInfo)
            {
                if (isAlreadySigned && !_dualCertificates.Contains(signInfo.Certificate))
                {
                    return new FileSignInfo(fullPath, hash, SignInfo.AlreadySigned, forceRepack:forceRepack);
                }

                // TODO: implement this check for native PE files as well:
                // extract copyright from native resource (.rsrc section) 
                if (signInfo.ShouldSign && peInfo != null && peInfo.IsManaged)
                {
                    bool isMicrosoftLibrary = IsMicrosoftLibrary(peInfo.Copyright);
                    bool isMicrosoftCertificate = !IsThirdPartyCertificate(signInfo.Certificate);
                    if (isMicrosoftLibrary != isMicrosoftCertificate)
                    {
                        if (isMicrosoftLibrary)
                        {
                            LogWarning(SigningToolErrorCode.SIGN001, $"Signing Microsoft library '{fullPath}' with 3rd party certificate '{signInfo.Certificate}'. The library is considered Microsoft library due to its copyright: '{peInfo.Copyright}'.");
                        }
                        else
                        {
                            LogWarning(SigningToolErrorCode.SIGN001, $"Signing 3rd party library '{fullPath}' with Microsoft certificate '{signInfo.Certificate}'. The library is considered 3rd party library due to its copyright: '{peInfo.Copyright}'.");
                        }
                    }
                }

                return new FileSignInfo(fullPath, hash, signInfo, (peInfo != null && peInfo.TargetFramework != "") ? peInfo.TargetFramework : null, forceRepack:forceRepack);
            }

            if (SignToolConstants.SignableExtensions.Contains(extension) || SignToolConstants.SignableOSXExtensions.Contains(extension))
            {
                // Extract the relative path inside the package / otherwise just return the full path of the file
                var contentHash = ContentUtil.GetContentHash(fullPath);
                var tempDir = Path.Combine(_pathToContainerUnpackingDirectory, ContentUtil.HashToString(contentHash));
                var relativePath = fullPath.Replace($@"{tempDir}\", "");
                LogError(SigningToolErrorCode.SIGN002, new SignedFileContentKey(contentHash, relativePath));
            }
            else
            {
                _log.LogMessage($"Ignoring non-signable file: {fullPath}");
            }

            return new FileSignInfo(fullPath, hash, SignInfo.Ignore, forceRepack: forceRepack);
        }

        private void LogWarning(SigningToolErrorCode code, string message)
            => _log.LogWarning(subcategory: null, warningCode: code.ToString(), helpKeyword: null, file: null, lineNumber: 0, columnNumber: 0, endLineNumber: 0, endColumnNumber: 0, message: message);

        private void LogError(SigningToolErrorCode code, SignedFileContentKey targetFile)
        {
            if (!_errors.TryGetValue(code, out var filesErrored))
            {
                filesErrored = new HashSet<SignedFileContentKey>();
            }

            filesErrored.Add(targetFile);
            _errors[code] = filesErrored;
        }

        /// <summary>
        /// Determines whether a library is a Microsoft library based on copyright.
        /// Copyright used for binary assets (assemblies and packages) built by Microsoft must be Microsoft copyright.
        /// </summary>
        private static bool IsMicrosoftLibrary(string copyright)
            => copyright.Contains("Microsoft");

        private static bool IsThirdPartyCertificate(string name)
            => name.Equals("3PartyDual", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("3PartySHA2", StringComparison.OrdinalIgnoreCase);

        private static PEInfo GetPEInfo(string fullPath)
        {
            bool isManaged = ContentUtil.IsManaged(fullPath);

            if (!isManaged)
            {
                return new PEInfo(isManaged);
            }

            bool isCrossgened = ContentUtil.IsCrossgened(fullPath);
            string publicKeyToken = ContentUtil.GetPublicKeyToken(fullPath);

            GetTargetFrameworkAndCopyright(fullPath, out string targetFramework, out string copyright);
            return new PEInfo(isManaged, isCrossgened, copyright, publicKeyToken, targetFramework);
        }

        private static void GetTargetFrameworkAndCopyright(string filePath, out string targetFramework, out string copyright)
        {
            targetFramework = string.Empty;
            copyright = string.Empty;

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
                            targetFramework = new FrameworkName(GetTargetFrameworkAttributeValue(metadataReader, attribute)).FullName;
                        }
                        else if (QualifiedNameEquals(metadataReader, attribute, "System.Reflection", "AssemblyCopyrightAttribute"))
                        {
                            copyright = GetTargetFrameworkAttributeValue(metadataReader, attribute);
                        }
                    }
                }
            }
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

            try
            {
                using (var archive = new ZipArchive(File.OpenRead(zipFileSignInfo.FullPath), ZipArchiveMode.Read))
                {
                    var nestedParts = new List<ZipPart>();

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relativePath = entry.FullName;

                        // `entry` might be just a pointer to a folder. We skip those.
                        if (relativePath.EndsWith("/") && entry.Name == "")
                        {
                            continue;
                        }

                        ImmutableArray<byte> contentHash;
                        using (var stream = entry.Open())
                        {
                            contentHash = ContentUtil.GetContentHash(stream);
                        }

                        var fileUniqueKey = new SignedFileContentKey(contentHash, relativePath);

                        if (!_whichPackagesTheFileIsIn.TryGetValue(fileUniqueKey, out var packages))
                        {
                            packages = new HashSet<string>();
                        }

                        packages.Add(zipFileSignInfo.FileName);
                        _whichPackagesTheFileIsIn[fileUniqueKey] = packages;

                        // if we already encountered file that hash the same content we can reuse its signed version when repackaging the container.
                        var fileName = Path.GetFileName(relativePath);
                        if (!_filesByContentKey.TryGetValue(new SignedFileContentKey(contentHash, fileName), out var fileSignInfo))
                        { 
                            string extractPathRoot = _useHashInExtractionPath ? ContentUtil.HashToString(contentHash) : _filesByContentKey.Count().ToString();
                            string tempPath = Path.Combine(_pathToContainerUnpackingDirectory, extractPathRoot, relativePath);
                            _log.LogMessage($"Extracting file '{fileName}' from '{zipFileSignInfo.FullPath}' to '{tempPath}'.");

                            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

                            using (var stream = entry.Open())
                            using (var tempFileStream = File.OpenWrite(tempPath))
                            {
                                stream.CopyTo(tempFileStream);
                            }

                            fileSignInfo = TrackFile(tempPath, contentHash, isNested: true);
                        }

                        if (fileSignInfo.SignInfo.ShouldSign || fileSignInfo.ForceRepack)
                        {
                            nestedParts.Add(new ZipPart(relativePath, fileSignInfo));
                        }
                    }

                    zipData = new ZipData(zipFileSignInfo, nestedParts.ToImmutableArray());

                    return true;
                }
            }
            catch (Exception e)
            {
                _log.LogErrorFromException(e);
                zipData = null;
                return false;
            }
        }
    }
}

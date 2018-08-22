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

                if (!IsManaged(fullPath))
                {
                    return new FileSignInfo(fullPath, new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2));
                }
                else
                {
                    var fileAsm = AssemblyName.GetAssemblyName(fullPath);
                    var pktBytes = fileAsm.GetPublicKeyToken();
                    var publicKeyToken = (pktBytes == null || pktBytes.Length == 0) ? string.Empty : string.Join("", pktBytes.Select(b => b.ToString("x2")));
                    var targetFramework = GetTargetFrameworkName(fullPath).FullName;
                    var fileName = Path.GetFileName(fullPath);

                    var keyForAllTargets = new ExplicitCertificateKey(fileName, publicKeyToken);
                    var keyForSpecificTarget = new ExplicitCertificateKey(fileName, publicKeyToken, targetFramework);

                    // Do we need to override the default certificate this file ?
                    if (_explicitCertificates.TryGetValue(keyForSpecificTarget, out var overridingCertificate) ||
                        _explicitCertificates.TryGetValue(keyForAllTargets, out overridingCertificate))
                    {
                        // If has overriding info, is it for ignoring the file?
                        if (overridingCertificate != null && overridingCertificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel))
                        {
                            return new FileSignInfo(fullPath, SignInfo.Ignore); // should ignore this file
                        }
                        // Otherwise, just use the overriding info if present
                    }

                    if (publicKeyToken == string.Empty)
                    {
                        if (string.IsNullOrEmpty(overridingCertificate))
                        {
                            _log.LogError($"SignInfo for file ({fileFullPath}) and empty PKT not found. Expected it to be informed in overriding infos.");
                            return SignInfo.Empty;
                        }

                        return new SignInfo(overridingCertificate, string.Empty);
                    }

                    if (_defaultSignInfoForPublicKeyToken.ContainsKey(publicKeyToken))
                    {
                        var signInfo = _defaultSignInfoForPublicKeyToken[publicKeyToken];
                        var certificate = overridingCertificate ?? signInfo.Certificate;

                        return new FileSignInfo(fullPath, new SignInfo(certificate, signInfo.StrongName, signInfo.ShouldIgnore, signInfo.IsAlreadySigned), targetFramework);
                    }

                    _log.LogError($"SignInfo for file '{fullPath}' with PublicKeyToken='{publicKeyToken}' not found.");
                    return default;
                }
            }
            else if (FileSignInfo.IsZipContainer(fullPath))
            {
                return new FileSignInfo(fullPath, new SignInfo(FileSignInfo.IsNupkg(fullPath) ? SignToolConstants.Certificate_NuGet : SignToolConstants.Certificate_VsixSHA2));
            }
            else
            {
                _log.LogWarning($"Unidentified artifact type: {fullPath}");
                return new FileSignInfo(fullPath, SignInfo.Ignore);
            }
        }

        private static bool IsManaged(string filePath)
        {
            try
            {
                AssemblyName.GetAssemblyName(filePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static FrameworkName GetTargetFrameworkName(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var pereader = new PEReader(stream))
            {
                if (pereader.HasMetadata)
                {
                    MetadataReader metadataReader = pereader.GetMetadataReader();

                    var attrs = metadataReader.GetAssemblyDefinition().GetCustomAttributes().Select(ah => metadataReader.GetCustomAttribute(ah));

                    foreach (var attr in attrs)
                    {
                        var ctorHandle = attr.Constructor;
                        if (ctorHandle.Kind != HandleKind.MemberReference)
                        {
                            continue;
                        }

                        var container = metadataReader.GetMemberReference((MemberReferenceHandle)ctorHandle).Parent;
                        var name = metadataReader.GetTypeReference((TypeReferenceHandle)container).Name;
                        if (!string.Equals(metadataReader.GetString(name), "TargetFrameworkAttribute"))
                        {
                            continue;
                        }

                        return new FrameworkName(GetFixedStringArguments(metadataReader, attr));
                    }
                }
            }

            return null;
        }

        private static string GetFixedStringArguments(MetadataReader reader, CustomAttribute attribute)
        {
            // Originally copied from here: https://github.com/Microsoft/msbuild/blob/a75c5a9e4f9ea6f2de24df4bfc2d60c09e684395/src/Tasks/AssemblyDependency/AssemblyInformation.cs#L353-L425

            var signature = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature;
            var signatureReader = reader.GetBlobReader(signature);
            var valueReader = reader.GetBlobReader(attribute.Value);

            var prolog = valueReader.ReadUInt16();
            if (prolog != 1)
            {
                // Invalid custom attribute prolog
                return null;
            }

            var header = signatureReader.ReadSignatureHeader();
            if (header.Kind != SignatureKind.Method || header.IsGeneric)
            {
                // Invalid custom attribute constructor signature
                return null;
            }

            if (!signatureReader.TryReadCompressedInteger(out var parameterCount))
            {
                // Invalid custom attribute constructor signature
                return null;
            }

            var returnType = signatureReader.ReadSignatureTypeCode();
            if (returnType != SignatureTypeCode.Void)
            {
                // Invalid custom attribute constructor signature
                return null;
            }

            // Custom attribute constructor must take only strings
            return valueReader.ReadSerializedString();
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

                    var partFileName = TrackFile(packagePartTempName);

                    nestedParts.Add(new ZipPart(relativePath, partFileName, null, partFileName.SignInfo));
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

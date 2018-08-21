using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
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
        /// </summary>
        private Dictionary<FileName, ZipData> _zipDataMap;

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
        private List<FileName> _filesToSign = new List<FileName>();

        public Configuration(string tempDir, string[] explicitSignList, Dictionary<string, SignInfo> mapPublicKeyTokenToSignInfo, Dictionary<ExplicitCertificateKey, string> overridingSigningInfo, string publishUri, TaskLoggingHelper log)
        {
            _pathToContainerUnpackingDirectory = Path.Combine(tempDir, "ZipArchiveUnpackingDirectory");
            _log = log;
            _publishUri = publishUri;
            _defaultSignInfoForPublicKeyToken = mapPublicKeyTokenToSignInfo;
            _explicitCertificates = overridingSigningInfo;
            _zipDataMap = new Dictionary<FileName, ZipData>();
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

        private FileName TrackFile(string fileFullPath)
        {
            var signInfo = ExtractSignInfo(fileFullPath);
            var fileName = new FileName(fileFullPath, signInfo);

            if (signInfo.IsAlreadySigned)
            {
                _log.LogMessage($"Ignoring already signed file: {fileFullPath}");
            }
            else if (signInfo.ShouldIgnore)
            {
                _log.LogMessage($"Ignoring signing for this file: {fileFullPath}");
            }
            else
            {
                if (FileName.IsZipContainer(fileFullPath))
                {
                    if (BuildZipData(fileName, out var zipData))
                    {
                        _zipDataMap[fileName] = zipData;
                    }
                }

                _filesToSign.Add(fileName);

                _log.LogMessage($"New file to sign: {fileName}");
            }

            return fileName;
        }

        private SignInfo ExtractSignInfo(string fileFullPath)
        {
            if (FileName.IsPEFile(fileFullPath))
            {
                using (var stream = File.OpenRead(fileFullPath))
                {
                    if (ContentUtil.IsAssemblyStrongNameSigned(stream))
                    {
                        return SignInfo.AlreadySigned;
                    }
                }

                if (!IsManaged(fileFullPath))
                {
                    return new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2, null);
                }
                else
                {
                    var fileAsm = System.Reflection.AssemblyName.GetAssemblyName(fileFullPath);
                    var pktBytes = fileAsm.GetPublicKeyToken();
                    var publicKeyToken = string.Join("", pktBytes.Select(b => b.ToString("x2")));
                    var targetFramework = GetTargetFrameworkName(fileFullPath).FullName;
                    var fileName = Path.GetFileName(fileFullPath);

                    var keyForAllTargets = new ExplicitCertificateKey(fileName, publicKeyToken, SignToolConstants.AllTargetFrameworksSentinel);
                    var keyForSpecificTarget = new ExplicitCertificateKey(fileName, publicKeyToken, targetFramework);

                    // Do we need to override the default certificate this file ?
                    if (_explicitCertificates.TryGetValue(keyForSpecificTarget, out var overridingCertificate) ||
                        _explicitCertificates.TryGetValue(keyForAllTargets, out overridingCertificate))
                    {
                        // If has overriding info, is it for ignoring the file?
                        if (overridingCertificate != null && overridingCertificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel))
                        {
                            return SignInfo.Ignore; // should ignore this file
                        }
                        // Otherwise, just use the overriding info if present
                    }

                    if (string.IsNullOrEmpty(publicKeyToken))
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

                        return new SignInfo(certificate, signInfo.StrongName, signInfo.ShouldIgnore, signInfo.IsEmpty, signInfo.IsAlreadySigned);
                    }

                    _log.LogError($"SignInfo for file ({fileFullPath}) with Public Key Token {publicKeyToken} not found.");
                    return SignInfo.Empty;
                }
            }
            else if (FileName.IsZipContainer(fileFullPath))
            {
                return new SignInfo(FileName.IsNupkg(fileFullPath) ? SignToolConstants.Certificate_NuGet : SignToolConstants.Certificate_VsixSHA2, null);
            }
            else
            {
                _log.LogWarning($"Unidentified artifact type: {fileFullPath}");
                return SignInfo.Ignore;
            }
        }

        private static bool IsManaged(string filePath)
        {
            try
            {
                System.Reflection.AssemblyName testAssembly = System.Reflection.AssemblyName.GetAssemblyName(filePath);

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
        private bool BuildZipData(FileName zipFileName, out ZipData zipData)
        {
            Debug.Assert(zipFileName.IsZipContainer());

            Package package = null;

            try
            {
                package = Package.Open(zipFileName.FullPath, FileMode.Open, FileAccess.Read);
                var packageTempDir = Path.Combine(_pathToContainerUnpackingDirectory, Guid.NewGuid().ToString());
                var nestedParts = new List<ZipPart>();

                foreach (var part in package.GetParts())
                {
                    var relativePath = GetPartRelativeFileName(part);
                    var packagePartTempName = Path.Combine(packageTempDir, relativePath);
                    var packagePartTempDirectory = Path.GetDirectoryName(packagePartTempName);

                    if (!FileName.IsZipContainer(packagePartTempName) && !FileName.IsPEFile(packagePartTempName))
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

                zipData = new ZipData(zipFileName, nestedParts.ToImmutableList());

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

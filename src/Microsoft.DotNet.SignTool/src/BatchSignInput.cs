// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    /// <summary>
    /// Represents all of the input to the batch signing process.
    /// </summary>
    internal sealed class BatchSignInput
    {
        private TaskLoggingHelper _log;

        /// <summary>
        /// Uri, to be consumed by later steps, which describes where these files get published to.
        /// </summary>
        internal string PublishUri { get; }

        /// <summary>
        /// This store content information for container files.
        /// </summary>
        internal Dictionary<FileName, ZipData> ZipDataMap { get; set; }

        /// <summary>
        /// Path to where container files will be extracted.
        /// </summary>
        private readonly string _pathToContainerUnpackingDirectory;

        /// <summary>
        /// This enable the overriding of the default certificate for a given file+token+target_framework.
        /// It also contains a SignToolConstants.IgnoreFileCertificateSentinel flag in the certificate name in case the file does not need to be signed
        /// for that 
        /// </summary>
        private readonly Dictionary<(string, string, string), string> _fileAndTokenToOverridingInfos;

        /// <summary>
        /// Used to look for signing information when we have the PublicKeyToken of a file.
        /// </summary>
        private readonly Dictionary<string, SignInfo> _mapTokenToSignInfo;

        /// <summary>
        /// A list of all of the binaries that MUST be signed.
        /// </summary>
        public List<FileName> FilesToSign = new List<FileName>();

        internal BatchSignInput(string tempDir, string[] explicitSignList, Dictionary<string, SignInfo> mapPublicKeyTokenToSignInfo, Dictionary<(string, string, string), string> overridingSigningInfo, string publishUri, TaskLoggingHelper log)
        {
            _pathToContainerUnpackingDirectory = Path.Combine(tempDir, "ZipArchiveUnpackingDirectory");
            _log = log;
            _mapTokenToSignInfo = mapPublicKeyTokenToSignInfo;
            _fileAndTokenToOverridingInfos = overridingSigningInfo;

            PublishUri = publishUri;
            ZipDataMap = new Dictionary<FileName, ZipData>();

            foreach (var fileNameToSign in explicitSignList)
            {
                TrackFile(fileNameToSign);
            }
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
                        ZipDataMap[fileName] = zipData;
                    }
                }

                FilesToSign.Add(fileName);

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

                if (IsManaged(fileFullPath) == true)
                {
                    var fileAsm = System.Reflection.AssemblyName.GetAssemblyName(fileFullPath);
                    var publicKeyToken = string.Join("", fileAsm.GetPublicKeyToken().Select(b => b.ToString("x2")));
                    var targetFramework = GetTargetFrameworkName(fileFullPath).FullName;
                    var justNameAndExtension = Path.GetFileName(fileFullPath);
                    string overridingCertificate = null;

                    var keyForAllTargets = (justNameAndExtension, publicKeyToken, SignToolConstants.AllTargetFrameworksSentinel);
                    var keyForSpecificTarget = (justNameAndExtension, publicKeyToken, targetFramework);

                    /// Do we need to override the default certificate this file ?
                    if (_fileAndTokenToOverridingInfos.TryGetValue(keyForAllTargets, out overridingCertificate) ||
                        _fileAndTokenToOverridingInfos.TryGetValue(keyForSpecificTarget, out overridingCertificate))
                    {
                        /// If has overriding info, is it for ignoring the file?
                        if (overridingCertificate != null && overridingCertificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel))
                        {
                            return SignInfo.Ignore; // should ignore this file
                        }
                        /// Otherwise, just use the overriding info if present
                    }
                
                    if (_mapTokenToSignInfo.ContainsKey(publicKeyToken))
                    {
                        var signInfo = new SignInfo(_mapTokenToSignInfo[publicKeyToken]);

                        signInfo.Certificate = overridingCertificate ?? signInfo.Certificate;

                        return signInfo;
                    }
                    else
                    {
                        _log.LogError($"SignInfo for Public Key Token {publicKeyToken} not found.");
                        return SignInfo.Empty;
                    }
                }
                else
                {
                    return new SignInfo(SignToolConstants.Certificate_MicrosoftSHA2, null);
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

        private static bool? IsManaged(string filePath)
        {
            try
            {
                System.Reflection.AssemblyName testAssembly = System.Reflection.AssemblyName.GetAssemblyName(filePath);

                return true;
            }
            catch (System.BadImageFormatException)
            {
                return false;
            }
            catch (Exception)
            {
                return null;
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

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.DotNet.SignTool.Json;
using static Microsoft.DotNet.SignTool.PathUtil;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class BatchSignUtil
    {
        internal static readonly StringComparer FilePathComparer = StringComparer.OrdinalIgnoreCase;

        private readonly TaskLoggingHelper _log;
        private readonly IBuildEngine _buildEngine;
        private readonly BatchSignInput _batchData;
        private readonly SignTool _signTool;
        private readonly ContentUtil _contentUtil = new ContentUtil();
        private readonly string _orchestrationManifestPath;
        private readonly string _unpackingDirectory;

        internal BatchSignUtil(IBuildEngine buildEngine, TaskLoggingHelper log, SignTool signTool, BatchSignInput batchData, string orchestrationManifestPath)
        {
            _signTool = signTool;
            _batchData = batchData;
            _orchestrationManifestPath = orchestrationManifestPath;
            _log = log;
            _buildEngine = buildEngine;

            // TODO: Better path; for now making sure the relative paths are all in the same "OutputDirectory" value should help things work.
            _unpackingDirectory = Path.Combine(batchData.OutputPath, "ZipArchiveUnpackingDirectory");
        }

        internal void Go()
        {
            // Build up all of our data structures. This will run a number of integrity checks to make sure the
            // data provided is correct / consistent.
            VerifyCertificates(_log);
            var contentMap = BuildContentMap(_log);
            var zipDataMap = BuildAllZipData(contentMap, _log);

            if (_log.HasLoggedErrors)
            {
                return;
            }

            // At this point we trust the content we're about to sign.  We can now take the information from _batchData
            // and put any content and zipDataMap hash values into the new manifest.
            if (!string.IsNullOrEmpty(_orchestrationManifestPath))
            {
                GenerateOrchestrationManifest(_batchData, contentMap, _orchestrationManifestPath);
                return;
            }

            // Next remove public signing from all of the assemblies; it can interfere with the signing process.
            RemovePublicSign();

            // Next sign all of the files
            if (!SignFiles(contentMap, zipDataMap))
            {
                return;
            }

            // Validate the signing worked and produced actual signed binaries in all locations.
            VerifyAfterSign(zipDataMap, _log);

            _log.LogMessage(MessageImportance.High, "Build artifacts signed and validated.");
        }

        private void GenerateOrchestrationManifest(BatchSignInput batchData, ContentMap contentMap, string outputPath)
        {
            _log.LogMessage(MessageImportance.High, $"Generating orchestration file manifest into {outputPath}");
            OrchestratedFileJson fileJsonWithInfo = new OrchestratedFileJson
            {
                ExcludeList = _batchData.ExternalFileNames.ToArray() ?? Array.Empty<string>()
            };

            var distinctSigningCombos = batchData.FileSignInfoMap.Values.GroupBy(v => new { v.Certificate, v.StrongName });

            List<OrchestratedFileSignData> newList = new List<OrchestratedFileSignData>();
            foreach (var combinationToSign in distinctSigningCombos)
            {
                var filesInThisGroup = combinationToSign.Select(c => new FileSignDataEntry()
                {
                    FilePath = c.FileName.RelativePath,
                    SHA256Hash = contentMap.GetChecksum(c.FileName),
                    PublishToFeedUrl = batchData.PublishUri
                });
                newList.Add(new OrchestratedFileSignData()
                {
                    Certificate = combinationToSign.Key.Certificate,
                    StrongName = combinationToSign.Key.StrongName,
                    FileList = filesInThisGroup.ToArray()
                });
            }
            fileJsonWithInfo.SignList = newList.ToArray();
            fileJsonWithInfo.Kind = "orchestration";

            using (StreamWriter file = File.CreateText(outputPath))
            {
                file.Write(JsonConvert.SerializeObject(fileJsonWithInfo, Formatting.Indented));
            }
        }

        private void RemovePublicSign()
        {
            foreach (var name in _batchData.AssemblyNames)
            {
                var fileSignInfo = _batchData.FileSignInfoMap[name];
                if (fileSignInfo.StrongName != null)
                {
                    _log.LogMessage($"Removing public sign: '{name}'");
                    _signTool.RemovePublicSign(name.FullPath);
                }
            }
        }

        /// <summary>
        /// Actually sign all of the described files.
        /// </summary>
        private bool SignFiles(ContentMap contentMap, Dictionary<FileName, ZipData> zipDataMap)
        {
            // Generate the list of signed files in a deterministic order. Makes it easier to track down
            // bugs if repeated runs use the same ordering.
            var toSignList = _batchData.FileNames.ToList();
            var round = 0;
            var signedSet = new HashSet<FileName>();

            bool signFiles(IEnumerable<FileName> files)
            {
                var filesToSign = files.Select(fileName => _batchData.FileSignInfoMap[fileName]).Where(info => !info.IsEmpty).ToArray();

                _log.LogMessage(MessageImportance.High, $"Signing Round {round}: {filesToSign.Length} files to sign.");
                foreach (var file in filesToSign)
                {
                    _log.LogMessage(MessageImportance.Low, $"File: '{file.FileName}'");
                }

                return _signTool.Sign(_buildEngine, round, filesToSign);
            }

            void repackFiles(IEnumerable<FileName> files)
            {
                foreach (var file in files)
                {
                    if (file.IsZipContainer)
                    {
                        _log.LogMessage(MessageImportance.Low, $"Repacking container: '{file}'");
                        Repack(zipDataMap[file]);
                    }
                }
            }

            // Is this file ready to be signed? That is are all of the items that it depends on already
            // signed?
            bool isReadyToSign(FileName fileName)
            {
                if (!fileName.IsZipContainer)
                {
                    return true;
                }

                var zipData = zipDataMap[fileName];
                return zipData.NestedParts.All(x => signedSet.Contains(x.FileName));
            }

            // Extract the next set of files that should be signed. This is the set of files for which all of the
            // dependencies have been signed.
            List<FileName> extractNextGroup()
            {
                var list = new List<FileName>();
                var i = 0;
                while (i < toSignList.Count)
                {
                    var current = toSignList[i];
                    if (isReadyToSign(current))
                    {
                        list.Add(current);
                        toSignList.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }

                return list;
            }

            while (toSignList.Count > 0)
            {
                var list = extractNextGroup();
                if (list.Count == 0)
                {
                    throw new InvalidOperationException("No progress made on signing which indicates a bug");
                }

                repackFiles(list);
                if (!signFiles(list))
                {
                    return false;
                }

                round++;
                list.ForEach(x => signedSet.Add(x));
            }

            return true;
        }

        /// <summary>
        /// Repack the VSIX with the signed parts from the binaries directory.
        /// </summary>
        private void Repack(ZipData vsixData)
        {
            using (var package = Package.Open(vsixData.Name.FullPath, FileMode.Open, FileAccess.ReadWrite))
            {
                foreach (var part in package.GetParts())
                {
                    var relativeName = GetPartRelativeFileName(part);
                    var vsixPart = vsixData.FindNestedBinaryPart(relativeName);
                    if (!vsixPart.HasValue)
                    {
                        continue;
                    }

                    using (var stream = File.OpenRead(vsixPart.Value.FileName.FullPath))
                    using (var partStream = part.GetStream(FileMode.Open, FileAccess.ReadWrite))
                    {
                        stream.CopyTo(partStream);
                        partStream.SetLength(stream.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Return all the assembly and VSIX contents nested in the VSIX
        /// </summary>
        private List<string> GetVsixPartRelativeNames(FileName vsixName)
        {
            var list = new List<string>();
            using (var package = Package.Open(vsixName.FullPath, FileMode.Open, FileAccess.Read))
            {
                foreach (var part in package.GetParts())
                {
                    var name = GetPartRelativeFileName(part);
                    list.Add(name);
                }
            }

            return list;
        }

        /// <summary>
        /// Build up a table of checksum to <see cref="FileName"/> instance map. This will report errors if it
        /// is unable to read any of the files off of disk.
        /// </summary>
        private ContentMap BuildContentMap(TaskLoggingHelper log)
        {
            var contentMap = new ContentMap();

            foreach (var fileName in _batchData.FileNames)
            {
                try
                {
                    if (string.IsNullOrEmpty(fileName.SHA256Hash))
                    {
                        var checksum = _contentUtil.GetChecksum(fileName.FullPath);
                        contentMap.Add(fileName, checksum);
                    }
                    else
                    {
                        if (File.Exists(fileName.FullPath))
                        {
                            contentMap.Add(fileName, fileName.SHA256Hash);
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!File.Exists(fileName.FullPath))
                    {
                        log.LogError($"Did not find {fileName} at {fileName.FullPath}");
                    }
                    else
                    {
                        log.LogError($"Unable to read content of {fileName.FullPath}: {ex.Message}");
                    }
                }
            }

            return contentMap;
        }

        /// <summary>
        /// Sanity check the certificates that are attached to the various items. Ensure we aren't using, say, a VSIX
        /// certificate on a DLL for example.
        /// </summary>
        private void VerifyCertificates(TaskLoggingHelper log)
        {
            foreach (var pair in _batchData.FileSignInfoMap.OrderBy(x => x.Key.RelativePath))
            {
                var fileName = pair.Key;
                var fileSignInfo = pair.Value;
                var isVsixCert = !string.IsNullOrEmpty(fileSignInfo.Certificate) && IsVsixCertificate(fileSignInfo.Certificate);

                if (fileName.IsAssembly)
                {
                    if (isVsixCert)
                    {
                        log.LogError($"Assembly {fileName} cannot be signed with a VSIX certificate");
                    }
                }
                else if (fileName.IsVsix)
                {
                    if (!isVsixCert)
                    {
                        log.LogError($"VSIX {fileName} must be signed with a VSIX certificate");
                    }

                    if (fileSignInfo.StrongName != null)
                    {
                        log.LogError($"VSIX {fileName} cannot be strong name signed");
                    }
                }
                else if (fileName.IsNupkg)
                {
                    if (fileSignInfo.StrongName != null || fileSignInfo.Certificate != null)
                    {
                        log.LogError($"Nupkg {fileName} cannot be strong name or certificate signed");
                    }
                }
            }
        }

        private Dictionary<FileName, ZipData> BuildAllZipData(ContentMap contentMap, TaskLoggingHelper log)
        {
            var zipDataMap = new Dictionary<FileName, ZipData>();
            foreach (var zipName in _batchData.FileNames.Where(x => x.IsZipContainer))
            {
                var data = BuildZipData(zipName, contentMap, log);
                zipDataMap[zipName] = data;
            }

            return zipDataMap;
        }

        /// <summary>
        /// Build up the <see cref="ZipData"/> instance for a given zip container. This will also report any consistency
        /// errors found when examining the zip archive.
        /// </summary>
        private ZipData BuildZipData(FileName zipFileName, ContentMap contentMap, TaskLoggingHelper log)
        {
            Debug.Assert(zipFileName.IsZipContainer);

            var nestedExternalBinaries = new List<string>();
            var nestedParts = new List<ZipPart>();
            using (var package = Package.Open(zipFileName.FullPath, FileMode.Open, FileAccess.Read))
            {
                foreach (var part in package.GetParts())
                {
                    var relativeName = GetPartRelativeFileName(part);
                    var name = Path.GetFileName(relativeName);
                    if (!IsZipContainer(name) && !IsAssembly(name))
                    {
                        continue;
                    }

                    if (_batchData.ExternalFileNames.Contains(name))
                    {
                        nestedExternalBinaries.Add(name);
                        continue;
                    }

                    if (!_batchData.FileNames.Any(x => FilePathComparer.Equals(x.Name, name)))
                    {
                        log.LogError($"Zip Container '{zipFileName}' has part '{relativeName}' which is not listed in the sign or external list");
                        continue;
                    }

                    // This represents a binary that we need to sign.  Ensure the content in the VSIX is the same as the
                    // content in the binaries directory by doing a checksum match.
                    using (var stream = part.GetStream())
                    {
                        string checksum = _contentUtil.GetChecksum(stream);
                        if (!contentMap.TryGetFileName(checksum, out var checksumName))
                        {
                            log.LogError($"{zipFileName} has part {relativeName} which does not match the content in the binaries directory");
                            continue;
                        }

                        if (!FilePathComparer.Equals(checksumName.Name, name))
                        {
                            log.LogError($"{zipFileName} has part {relativeName} with a different name in the binaries directory: {checksumName}");
                            continue;
                        }

                        nestedParts.Add(new ZipPart(relativeName, checksumName, checksum));
                    }
                }
            }

            return new ZipData(zipFileName, nestedParts.ToImmutableArray(), nestedExternalBinaries.ToImmutableArray());
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

        private void VerifyAfterSign(Dictionary<FileName, ZipData> zipDataMap, TaskLoggingHelper log)
        {
            foreach (var fileName in _batchData.FileNames)
            {
                if (fileName.IsAssembly)
                {
                    using (var stream = File.OpenRead(fileName.FullPath))
                    {
                        if (!_signTool.VerifySignedAssembly(stream))
                        {
                            log.LogError($"Assembly {fileName} is not signed properly");
                        }
                    }
                }
                else if (fileName.IsZipContainer)
                {
                    var zipData = zipDataMap[fileName];
                    using (var package = Package.Open(fileName.FullPath, FileMode.Open, FileAccess.Read))
                    {
                        foreach (var part in package.GetParts())
                        {
                            var relativeName = GetPartRelativeFileName(part);
                            var zipPart = zipData.FindNestedBinaryPart(relativeName);
                            if (!zipPart.HasValue || !zipPart.Value.FileName.IsAssembly)
                            {
                                continue;
                            }

                            using (var stream = part.GetStream())
                            {
                                if (!_signTool.VerifySignedAssembly(stream))
                                {
                                    log.LogError($"Zip container {fileName} has part {relativeName} which is not signed.");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IsVsixCertificate(string certificate) => certificate.StartsWith("Vsix", StringComparison.OrdinalIgnoreCase);
    }
}

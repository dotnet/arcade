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
using SignTool.Json;
using static SignTool.PathUtil;

namespace SignTool
{
    internal sealed class BatchSignUtil
    {
        internal static readonly StringComparer FilePathComparer = StringComparer.OrdinalIgnoreCase;

        private readonly BatchSignInput _batchData;
        private readonly ISignTool _signTool;
        private readonly ContentUtil _contentUtil = new ContentUtil();
        private readonly string _orchestrationManifestPath;
        private readonly string _unpackingDirectory;

        internal BatchSignUtil(ISignTool signTool, BatchSignInput batchData, string orchestrationManifestPath)
        {
            _signTool = signTool;
            _batchData = batchData;
            _orchestrationManifestPath = orchestrationManifestPath;
            // TODO: Better path; for now making sure the relative paths are all in the same "OutputDirectory" value should help things work.
            _unpackingDirectory = Path.Combine(batchData.OutputPath, "ZipArchiveUnpackingDirectory");
        }

        internal bool Go(TextWriter textWriter)
        {
            // Build up all of our data structures. This will run a number of integrity checks to make sure the
            // data provided is correct / consistent.
            var allGood = VerifyCertificates(textWriter);
            var contentMap = BuildContentMap(textWriter, ref allGood);
            var zipDataMap = BuildAllZipData(contentMap, textWriter, ref allGood);

            if (!allGood)
            {
                return false;
            }
            // At this point we trust the content we're about to sign.  We can now take the information from _batchData
            // and put any content and zipDataMap hash values into the new manifest.
            if (!string.IsNullOrEmpty(_orchestrationManifestPath))
            {
                textWriter.WriteLine($"Writing updated SignTool Data Json information to '{_orchestrationManifestPath}'");
                return GenerateOrchestrationManifest(textWriter, _batchData, contentMap, _orchestrationManifestPath);
            }

            // Next remove public signing from all of the assemblies; it can interfere with the signing process.
            RemovePublicSign(textWriter);

            // Next sign all of the files
            SignFiles(contentMap, zipDataMap, textWriter);

            // Validate the signing worked and produced actual signed binaries in all locations.
            return VerifyAfterSign(zipDataMap, textWriter);
        }

        private bool GenerateOrchestrationManifest(TextWriter textWriter, BatchSignInput batchData, ContentMap contentMap, string outputPath)
        {
            textWriter.WriteLine($"Generating orchestration file manifest into {outputPath}");
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

            return true;
        }

        private void RemovePublicSign(TextWriter textWriter)
        {
            textWriter.WriteLine("Removing public signing");
            foreach (var name in _batchData.AssemblyNames)
            {
                var fileSignInfo = _batchData.FileSignInfoMap[name];
                if (fileSignInfo.StrongName != null)
                {
                    textWriter.WriteLine($"\t{name}");
                    _signTool.RemovePublicSign(name.FullPath);
                }
            }
        }

        /// <summary>
        /// Actually sign all of the described files.
        /// </summary>
        private void SignFiles(ContentMap contentMap, Dictionary<FileName, ZipData> zipDataMap, TextWriter textWriter)
        {
            // Generate the list of signed files in a deterministic order. Makes it easier to track down
            // bugs if repeated runs use the same ordering.
            var toSignList = _batchData.FileNames.ToList();
            var round = 0;
            var signedSet = new HashSet<FileName>();

            void signFiles(IEnumerable<FileName> files)
            {
                textWriter.WriteLine($"Signing Round {round}");
                foreach (var name in files)
                {
                    textWriter.WriteLine($"\t{name}");
                }
                _signTool.Sign(files.Select(x => _batchData.FileSignInfoMap[x]).Where(x => !x.IsEmpty), textWriter);
            }

            void repackFiles(IEnumerable<FileName> files)
            {
                var any = false;
                foreach (var file in files)
                {
                    if (file.IsZipContainer)
                    {
                        if (!any)
                        {
                            textWriter.WriteLine("Repacking");
                            any = true;
                        }

                        textWriter.WriteLine($"\t{file}");
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
                    throw new Exception("No progress made on signing which indicates a bug");
                }

                repackFiles(list);
                signFiles(list);
                round++;
                list.ForEach(x => signedSet.Add(x));
            }
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
        private ContentMap BuildContentMap(TextWriter textWriter, ref bool allGood)
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
                        textWriter.WriteLine($"Did not find {fileName} at {fileName.FullPath}");
                    }
                    else
                    {
                        textWriter.WriteLine($"Unable to read content of {fileName.FullPath}: {ex.Message}");
                    }
                    allGood = false;
                }
            }

            return contentMap;
        }

        /// <summary>
        /// Sanity check the certificates that are attached to the various items. Ensure we aren't using, say, a VSIX
        /// certificate on a DLL for example.
        /// </summary>
        private bool VerifyCertificates(TextWriter textWriter)
        {
            var allGood = true;
            foreach (var pair in _batchData.FileSignInfoMap.OrderBy(x => x.Key.RelativePath))
            {
                var fileName = pair.Key;
                var fileSignInfo = pair.Value;
                var isVsixCert = !string.IsNullOrEmpty(fileSignInfo.Certificate) && IsVsixCertificate(fileSignInfo.Certificate);

                if (fileName.IsAssembly)
                {
                    if (isVsixCert)
                    {
                        textWriter.WriteLine($"Assembly {fileName} cannot be signed with a VSIX certificate");
                        allGood = false;
                    }
                }
                else if (fileName.IsVsix)
                {
                    if (!isVsixCert)
                    {
                        textWriter.WriteLine($"VSIX {fileName} must be signed with a VSIX certificate");
                        allGood = false;
                    }

                    if (fileSignInfo.StrongName != null)
                    {
                        textWriter.WriteLine($"VSIX {fileName} cannot be strong name signed");
                        allGood = false;
                    }
                }
                else if (fileName.IsNupkg)
                {
                    if (fileSignInfo.StrongName != null || fileSignInfo.Certificate != null)
                    {
                        textWriter.WriteLine($"Nupkg {fileName} cannot be strong name or certificate signed");
                        allGood = false;
                    }
                }
            }

            return allGood;
        }

        private Dictionary<FileName, ZipData> BuildAllZipData(ContentMap contentMap, TextWriter textWriter, ref bool allGood)
        {
            var zipDataMap = new Dictionary<FileName, ZipData>();
            foreach (var zipName in _batchData.FileNames.Where(x => x.IsZipContainer))
            {
                var data = BuildZipData(zipName, contentMap, textWriter, ref allGood);
                zipDataMap[zipName] = data;
            }

            return zipDataMap;
        }

        /// <summary>
        /// Build up the <see cref="ZipData"/> instance for a given zip container. This will also report any consistency
        /// errors found when examining the zip archive.
        /// </summary>
        private ZipData BuildZipData(FileName zipFileName, ContentMap contentMap, TextWriter textWriter, ref bool allGood)
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
                        allGood = false;
                        textWriter.WriteLine($"Zip Container '{zipFileName}' has part '{name}' which is not listed in the sign or external list");
                        continue;
                    }

                    // This represents a binary that we need to sign.  Ensure the content in the VSIX is the same as the
                    // content in the binaries directory by doing a chekcsum match.
                    using (var stream = part.GetStream())
                    {
                        string checksum = _contentUtil.GetChecksum(stream);
                        if (!contentMap.TryGetFileName(checksum, out var checksumName))
                        {
                            allGood = false;
                            textWriter.WriteLine($"{zipFileName} has part {name} which does not match the content in the binaries directory");
                            continue;
                        }

                        if (!FilePathComparer.Equals(checksumName.Name, name))
                        {
                            allGood = false;
                            textWriter.WriteLine($"{zipFileName} has part {name} with a different name in the binaries directory: {checksumName}");
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

        private bool VerifyAfterSign(Dictionary<FileName, ZipData> zipDataMap, TextWriter textWriter)
        {
            var allGood = true;
            foreach (var fileName in _batchData.FileNames)
            {
                if (fileName.IsAssembly)
                {
                    using (var stream = File.OpenRead(fileName.FullPath))
                    {
                        if (!_signTool.VerifySignedAssembly(stream))
                        {
                            textWriter.WriteLine($"Assembly {fileName} is not signed properly");
                            allGood = false;
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
                                    textWriter.WriteLine($"Zip container {fileName} has part {relativeName} which is not signed.");
                                    allGood = false;
                                }
                            }
                        }
                    }
                }
            }

            return allGood;
        }

        private static bool IsVsixCertificate(string certificate) => certificate.StartsWith("Vsix", StringComparison.OrdinalIgnoreCase);
    }
}

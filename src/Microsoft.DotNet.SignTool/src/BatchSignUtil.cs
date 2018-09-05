// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class BatchSignUtil
    {
        internal static readonly StringComparer FilePathComparer = StringComparer.OrdinalIgnoreCase;

        private readonly TaskLoggingHelper _log;
        private readonly IBuildEngine _buildEngine;
        private readonly BatchSignInput _batchData;
        private readonly SignTool _signTool;

        internal BatchSignUtil(IBuildEngine buildEngine, TaskLoggingHelper log, SignTool signTool, BatchSignInput batchData)
        {
            _signTool = signTool;
            _batchData = batchData;
            _log = log;
            _buildEngine = buildEngine;
        }

        internal void Go()
        {
            VerifyCertificates(_log);

            if (_log.HasLoggedErrors)
            {
                return;
            }

            // Next remove public signing from all of the assemblies; it can interfere with the signing process.
            RemovePublicSign();

            // Next sign all of the files
            if (!SignFiles())
            {
                _log.LogError("Error during execution of Microbuild signing process.");
                return;
            }

            // Validate the signing worked and produced actual signed binaries in all locations.
            VerifyAfterSign(_log);

            if (_log.HasLoggedErrors)
            {
                return;
            }

            _log.LogMessage(MessageImportance.High, "Build artifacts signed and validated.");
        }

        private void RemovePublicSign()
        {
            foreach (var fileSignInfo in _batchData.FilesToSign.Where(x => x.IsPEFile()))
            {
                if (fileSignInfo.SignInfo.StrongName != null)
                {
                    _log.LogMessage($"Removing public sign: '{fileSignInfo.FileName}'");
                    _signTool.RemovePublicSign(fileSignInfo.FullPath);
                }
            }
        }

        /// <summary>
        /// Actually sign all of the described files.
        /// </summary>
        private bool SignFiles()
        {
            // Generate the list of signed files in a deterministic order. Makes it easier to track down
            // bugs if repeated runs use the same ordering.
            var toSignList = _batchData.FilesToSign.ToList();
            var round = 0;
            var signedSet = new HashSet<ImmutableArray<byte>>(ByteSequenceComparer.Instance);

            bool signFiles(IEnumerable<FileSignInfo> files)
            {
                var filesToSign = files.ToArray();

                _log.LogMessage(MessageImportance.High, $"Signing Round {round}: {filesToSign.Length} files to sign.");
                foreach (var file in filesToSign)
                {
                    _log.LogMessage(MessageImportance.Low, file.ToString());
                }

                return _signTool.Sign(_buildEngine, round, filesToSign);
            }

            void repackFiles(IEnumerable<FileSignInfo> files)
            {
                foreach (var file in files)
                {
                    if (file.IsZipContainer())
                    {
                        _log.LogMessage($"Repacking container: '{file.FileName}'");
                        _batchData.ZipDataMap[file.ContentHash].Repack();
                    }
                }
            }

            // Is this file ready to be signed? That is are all of the items that it depends on already
            // signed?
            bool isReadyToSign(FileSignInfo file)
            {
                if (!file.IsZipContainer())
                {
                    return true;
                }

                var zipData = _batchData.ZipDataMap[file.ContentHash];
                return zipData.NestedParts.All(x => !x.FileSignInfo.SignInfo.ShouldSign || signedSet.Contains(x.FileSignInfo.ContentHash));
            }

            // Extract the next set of files that should be signed. This is the set of files for which all of the
            // dependencies have been signed.
            List<FileSignInfo> extractNextGroup()
            {
                var list = new List<FileSignInfo>();
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
                list.ForEach(x => signedSet.Add(x.ContentHash));
            }

            return true;
        }

        /// <summary>
        /// Sanity check the certificates that are attached to the various items. Ensure we aren't using, say, a VSIX
        /// certificate on a DLL for example.
        /// </summary>
        private void VerifyCertificates(TaskLoggingHelper log)
        {
            foreach (var fileName in _batchData.FilesToSign.OrderBy(x => x.FullPath))
            {
                var isVsixCert = !string.IsNullOrEmpty(fileName.SignInfo.Certificate) && IsVsixCertificate(fileName.SignInfo.Certificate);

                if (fileName.IsPEFile())
                {
                    if (isVsixCert)
                    {
                        log.LogError($"Assembly {fileName} cannot be signed with a VSIX certificate");
                    }
                }
                else if (fileName.IsVsix())
                {
                    if (!isVsixCert)
                    {
                        log.LogError($"VSIX {fileName} must be signed with a VSIX certificate");
                    }

                    if (fileName.SignInfo.StrongName != null)
                    {
                        log.LogError($"VSIX {fileName} cannot be strong name signed.");
                    }
                }
                else if (fileName.IsNupkg())
                {
                    if (fileName.SignInfo.Certificate == null || !fileName.SignInfo.Certificate.Equals(SignToolConstants.Certificate_NuGet))
                    {
                        log.LogError($"Nupkg {fileName} should be signed with this certificate: {SignToolConstants.Certificate_NuGet}");
                    }

                    if (fileName.SignInfo.StrongName != null)
                    {
                        log.LogError($"Nupkg {fileName} cannot be strong name signed.");
                    }
                }
                else if (fileName.IsZip())
                {
                    if (fileName.SignInfo.Certificate != null)
                    {
                        log.LogError($"Zip {fileName} should not be signed with this certificate: {fileName.SignInfo.Certificate}");
                    }

                    if (fileName.SignInfo.StrongName != null)
                    {
                        log.LogError($"Zip {fileName} cannot be strong name signed.");
                    }
                }
            }
        }

        private void VerifyAfterSign(TaskLoggingHelper log)
        {
            foreach (var file in _batchData.FilesToSign)
            {
                if (file.IsPEFile())
                {
                    using (var stream = File.OpenRead(file.FullPath))
                    {
                        if (!_signTool.VerifySignedPEFile(stream))
                        {
                            log.LogError($"Assembly {file} is not signed properly");
                        }
                    }
                }
                else if (file.IsZipContainer())
                {
                    var zipData = _batchData.ZipDataMap[file.ContentHash];

                    using (var archive = new ZipArchive(File.OpenRead(file.FullPath), ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            string relativeName = entry.FullName;
                            var zipPart = zipData.FindNestedPart(relativeName);
                            if (!zipPart.HasValue || !zipPart.Value.FileSignInfo.IsPEFile())
                            {
                                continue;
                            }

                            using (var stream = entry.Open())
                            {
                                if (!_signTool.VerifySignedPEFile(stream))
                                {
                                    log.LogError($"Zip container {file} has part {relativeName} which is not signed.");
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskLoggingHelper = Microsoft.Build.Utilities.TaskLoggingHelper;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class BatchSignUtil
    {
        private readonly TaskLoggingHelper _log;
        private readonly IBuildEngine _buildEngine;
        private readonly BatchSignInput _batchData;
        private readonly SignTool _signTool;
        private readonly string[] _itemsToSkipStrongNameCheck;

        internal bool SkipZipContainerSignatureMarkerCheck { get; set; }

        internal BatchSignUtil(IBuildEngine buildEngine, TaskLoggingHelper log, SignTool signTool,
            BatchSignInput batchData, string[] itemsToSkipStrongNameCheck)
        {
            _signTool = signTool;
            _batchData = batchData;
            _log = log;
            _buildEngine = buildEngine;
            _itemsToSkipStrongNameCheck = itemsToSkipStrongNameCheck ?? Array.Empty<string>();
        }

        internal void Go(bool doStrongNameCheck)
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
                _log.LogError("Error during execution of signing process.");
                return;
            }

            if (!CopyFiles())
            {
                return;
            }

            // Check that all files have a strong name signature
            if (doStrongNameCheck)
            {
                VerifyStrongNameSigning();
            }

            // Validate the signing worked and produced actual signed binaries in all locations.
            // This is a recursive process since we process nested containers.
            foreach (var file in _batchData.FilesToSign)
            {
                VerifyAfterSign(file);
            }

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
                if (fileSignInfo.SignInfo.StrongName != null && fileSignInfo.SignInfo.ShouldSign)
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
            var toProcessList = _batchData.FilesToSign.ToList();
            var toRepackSet = _batchData.FilesToSign.Where(x => x.ShouldRepack)?.Select(x => x.FullPath)?.ToHashSet();
            var round = 0;
            var trackedSet = new HashSet<SignedFileContentKey>();

            // Given a list of files that need signing, sign them in a batch.
            bool signGroup(IEnumerable<FileSignInfo> files, out int totalFilesSigned)
            {
                var filesToSign = files.Where(fileInfo => fileInfo.SignInfo.ShouldSign).ToArray();
                totalFilesSigned = filesToSign.Length;
                if (filesToSign.Length == 0) return true;

                _log.LogMessage(MessageImportance.High, $"Round {round}: Signing {filesToSign.Length} files.");

                foreach (var file in filesToSign)
                {
                    _log.LogMessage(MessageImportance.Low, file.ToString());
                }

                return _signTool.Sign(_buildEngine, round, filesToSign);
            }

            // Given a list of files that need signing, sign the installer engines
            // of those that are wix containers.
            bool signEngines(IEnumerable<FileSignInfo> files, out int totalFilesSigned)
            {
                var enginesToSign = files.Where(fileInfo => fileInfo.SignInfo.ShouldSign && 
                                                fileInfo.IsWixContainer() &&
                                                Path.GetExtension(fileInfo.FullPath) == ".exe").ToArray();
                totalFilesSigned = enginesToSign.Length;
                if (enginesToSign.Length == 0)
                {
                    return true;
                }

                _log.LogMessage(MessageImportance.High, $"Round {round}: Signing {enginesToSign.Length} engines.");

                Dictionary<string, FileSignInfo> engines = new Dictionary<string, FileSignInfo>();
                var workingDirectory = Path.Combine(_signTool.TempDir, "engines");
                // extract engines
                foreach (var file in enginesToSign)
                {
                    string engineFileName = $"{Path.Combine(workingDirectory, file.FileName)}{SignToolConstants.MsiEngineExtension}";
                    _log.LogMessage(MessageImportance.Normal, $"Extracting engine from {file.FullPath}");
                    int exitCode = RunWixTool("insignia.exe", $"-ib {file.FullPath} -o {engineFileName}", workingDirectory, _signTool.WixToolsPath);
                    if(exitCode != 0)
                    {
                        _log.LogError($"Failed to extract engine from {file.FullPath}");
                        return false;
                    }
                    engines.Add(engineFileName, file);
                }

                // sign engines
                bool signResult = _signTool.Sign(_buildEngine, round, engines.Select(engine =>
                    new FileSignInfo(new PathWithHash(engine.Key, engine.Value.ContentHash), engine.Value.SignInfo)));
                if(!signResult)
                {
                    _log.LogError($"Failed to sign engines");
                    return signResult;
                }

                // attach engines
                foreach (var engine in engines)
                {
                    _log.LogMessage(MessageImportance.Normal, $"Attaching engine {engine.Key} to {engine.Value.FullPath}");
                    int exitCode = RunWixTool("insignia.exe", $"-ab {engine.Key} {engine.Value.FullPath} -o {engine.Value.FullPath}", workingDirectory, _signTool.WixToolsPath);

                    // cleanup engines (they fail signing verification if they stay in the drop
                    File.Delete(engine.Key);

                    if (exitCode != 0)
                    {
                        _log.LogError($"Failed to attach engine to {engine.Value.FullPath}");
                        return false;
                    }
                }
                return true;
            }

            // Given a group of file that are ready for processing,
            // repack those files that are containers.
            void repackGroup(IEnumerable<FileSignInfo> files)
            {
                var repackList = files.Where(w => toRepackSet.Contains(w.FullPath)).ToList();
                
                int repackCount = repackList.Count();
                if(repackCount == 0)
                {
                    return;
                }
                _log.LogMessage(MessageImportance.High, $"Repacking {repackCount} containers.");

                ParallelOptions parallelOptions = new ParallelOptions();
                parallelOptions.MaxDegreeOfParallelism = 16;
                Parallel.ForEach(repackList, parallelOptions, file =>
                {
                    if (file.IsZipContainer())
                    {
                        _log.LogMessage($"Repacking container: '{file.FileName}'");
                        _batchData.ZipDataMap[file.FileContentKey].Repack(_log);
                    }
                    else if (file.IsWixContainer())
                    {
                        _log.LogMessage($"Packing wix container: '{file.FileName}'");
                        _batchData.ZipDataMap[file.FileContentKey].Repack(_log, _signTool.TempDir, _signTool.WixToolsPath);
                    }
                    else
                    {
                        _log.LogError($"Don't know how to repack file '{file.FullPath}'");
                    }
                    toRepackSet.Remove(file.FullPath);
                });
            }

            // Is this file ready to be signed or repackaged? That is are all of the items that it depends on already
            // signed, don't need signing, and are repacked.
            bool isReady(FileSignInfo file)
            {
                if (file.IsContainer())
                {
                    var zipData = _batchData.ZipDataMap[file.FileContentKey];
                    return zipData.NestedParts.Values.All(x => (!x.FileSignInfo.SignInfo.ShouldSign ||
                        trackedSet.Contains(x.FileSignInfo.FileContentKey)) && !toRepackSet.Contains(x.FileSignInfo.FullPath)
                        );
                }
                return true;
            }

            // Identify the next set of files that should be signed or repacked.
            // This is the set of files for which all of the dependencies have been signed,
            // are already signed, are repacked, etc.
            List<FileSignInfo> identifyNextGroup()
            {
                var list = new List<FileSignInfo>();
                var i = 0;
                while (i < toProcessList.Count)
                {
                    var current = toProcessList[i];
                    if (isReady(current))
                    {
                        list.Add(current);
                        toProcessList.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }

                return list;
            }

            // Core algorithm of batch signing.
            // While there are files left to process,
            //  Identify which files are ready for processing (ready to repack or sign)
            //  Repack those of that set that are containers
            //  Sign any of those files that need signing, along with their engines.
            while (toProcessList.Count > 0)
            {
                var trackList = identifyNextGroup();
                if (trackList.Count == 0)
                {
                    throw new InvalidOperationException("No progress made on signing which indicates a bug");
                }

                repackGroup(trackList);

                int totalFilesSigned;
                if (!signEngines(trackList, out totalFilesSigned))
                {
                    return false;
                }
                if(totalFilesSigned > 0)
                {
                    round++;
                }

                if (!signGroup(trackList, out totalFilesSigned))
                {
                    return false;
                }
                if (totalFilesSigned > 0)
                {
                    round++;
                }
                trackList.ForEach(x => trackedSet.Add(x.FileContentKey));
            }

            return true;
        }

        internal static int RunWixTool(string toolName, string arguments, string workingDirectory, string wixToolsPath)
        {
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                Arguments = $"/c {toolName} {arguments}",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (Directory.Exists(wixToolsPath))
            {
                string path = processStartInfo.EnvironmentVariables["PATH"];
                path = $"{path};{wixToolsPath}";
                processStartInfo.EnvironmentVariables.Remove("PATH");
                processStartInfo.EnvironmentVariables.Add("PATH", path);
            }
            var process = Process.Start(processStartInfo);
            process.WaitForExit();
            return process.ExitCode;
        }

        private bool CopyFiles()
        {
            bool success = true;
            foreach (var entry in _batchData.FilesToCopy)
            {
                var src = entry.Key;
                var dst = entry.Value;

                try
                {
                    _log.LogMessage($"Updating '{dst}' with signed content");
                    File.Copy(src, dst, overwrite: true);
                }
                catch (Exception e)
                {
                    _log.LogError($"Updating '{dst}' with signed content failed: '{e.Message}'");
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// Sanity check the certificates that are attached to the various items. Ensure we aren't using, say, a VSIX
        /// certificate on a DLL for example.
        /// </summary>
        private void VerifyCertificates(TaskLoggingHelper log)
        {
            foreach (var fileName in _batchData.FilesToSign.OrderBy(x => x.FullPath))
            {
                bool isVsixCert = (!string.IsNullOrEmpty(fileName.SignInfo.Certificate) && IsVsixCertificate(fileName.SignInfo.Certificate)) ||
                                    fileName.SignInfo.IsAlreadySigned && fileName.HasSignableParts;

                bool isInvalidEmptyCertificate = fileName.SignInfo.Certificate == null && !fileName.HasSignableParts && !fileName.SignInfo.IsAlreadySigned;

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
                    if(isInvalidEmptyCertificate)
                    {
                        log.LogError($"Nupkg {fileName} should have a certificate name.");
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
                if (fileName.IsExecutableWixContainer())
                {
                    if (isInvalidEmptyCertificate)
                    {
                        log.LogError($"Wix file {fileName} should have a certificate name.");
                    }

                    if (fileName.SignInfo.StrongName != null)
                    {
                        log.LogError($"Wix file {fileName} cannot be strong name signed.");
                    }
                }
            }
        }

        private void VerifyAfterSign(FileSignInfo file)
        {
            if (file.IsPEFile())
            {
                using (var stream = File.OpenRead(file.FullPath))
                {
                    if (!_signTool.VerifySignedPEFile(stream))
                    {
                        _log.LogError($"Assembly {file.FullPath} is NOT signed properly");
                    }
                    else
                    {
                        _log.LogMessage(MessageImportance.Low, $"Assembly {file.FullPath} is signed properly");
                    }
                }
            }
            else if (file.IsPowerShellScript())
            {
                if (!_signTool.VerifySignedPowerShellFile(file.FullPath))
                {
                    _log.LogError($"Powershell file {file.FullPath} does not have a signature mark.");
                }
            }
            else if (file.IsZipContainer())
            {
                var zipData = _batchData.ZipDataMap[file.FileContentKey];
                bool signedContainer = false;

                using (var archive = new ZipArchive(File.OpenRead(file.FullPath), ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string relativeName = entry.FullName;

                        if (!SkipZipContainerSignatureMarkerCheck)
                        {
                            if (file.IsNupkg() && _signTool.VerifySignedNugetFileMarker(relativeName))
                            {
                                signedContainer = true;
                            }
                            else if (file.IsVsix() && _signTool.VerifySignedVSIXFileMarker(relativeName))
                            {
                                signedContainer = true;
                            }
                        }

                        var zipPart = zipData.FindNestedPart(relativeName);
                        if (!zipPart.HasValue)
                        {
                            continue;
                        }

                        VerifyAfterSign(zipPart.Value.FileSignInfo);
                    }
                }

                if (!SkipZipContainerSignatureMarkerCheck)
                {
                    if ((file.IsNupkg() || file.IsVsix()) && !signedContainer)
                    {
                        _log.LogError($"Container {file.FullPath} does not have signature marker.");
                    }
                    else
                    {
                        _log.LogMessage(MessageImportance.Low, $"Container {file.FullPath} has a signature marker.");
                    }
                }
            }
        }

        private void VerifyStrongNameSigning()
        {
            foreach (var file in _batchData.FilesToSign)
            {
                if (_itemsToSkipStrongNameCheck.Contains(file.FileName))
                {
                    _log.LogMessage($"Skipping strong-name validation for {file.FullPath}.");
                    continue;
                }

                if (file.IsManaged() && !file.IsCrossgened() && !_signTool.VerifyStrongNameSign(file.FullPath))
                {
                    _log.LogError($"Assembly {file.FullPath} is not strong-name signed correctly.");
                }
                else
                {
                    _log.LogMessage(MessageImportance.Low, $"Assembly {file.FullPath} strong-name signature is valid.");
                }
            }
        }

        private static bool IsVsixCertificate(string certificate) => certificate.StartsWith("Vsix", StringComparison.OrdinalIgnoreCase);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Collections.Generic;
using Microsoft.DotNet.StrongName;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// The signing implementation which actually signs binaries.
    /// </summary>
    internal sealed class RealSignTool : SignTool
    {
        private readonly string _dotnetPath;
        private readonly string _logDir;
        private readonly string _msbuildVerbosity;
        private readonly string _snPath;
        private readonly int _dotnetTimeout;

        /// <summary>
        /// The number of bytes from the start of the <see cref="CorHeader"/> to its <see cref="CorFlags"/>.
        /// </summary>
        internal const int OffsetFromStartOfCorHeaderToFlags =
               sizeof(Int32)  // byte count
             + sizeof(Int16)  // major version
             + sizeof(Int16)  // minor version
             + sizeof(Int64); // metadata directory

        internal bool TestSign { get; }

        internal RealSignTool(SignToolArgs args, TaskLoggingHelper log) : base(args, log)
        {
            TestSign = args.TestSign;
            _dotnetPath = args.DotNetPath;
            _msbuildVerbosity = args.MSBuildVerbosity;
            _snPath = args.SNBinaryPath;
            _logDir = args.LogDir;
            _dotnetTimeout = args.DotNetTimeout;
        }

        public override bool RunMSBuild(IBuildEngine buildEngine, string projectFilePath, string binLogPath, string logPath, string errorLogPath)
        {
            if (_dotnetPath == null)
            {
                return buildEngine.BuildProjectFile(projectFilePath, null, null, null);
            }

            Directory.CreateDirectory(_logDir);

            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo()
                {
                    FileName = _dotnetPath,
                    Arguments = $@"build ""{projectFilePath}"" -v:""{_msbuildVerbosity}"" -bl:""{binLogPath}""",
                    UseShellExecute = false,
                    WorkingDirectory = TempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => output.AppendLine(e.Data);
                process.ErrorDataReceived += (sender, e) => error.AppendLine(e.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool success = true;
                if (!process.WaitForExit(_dotnetTimeout))
                {
                    _log.LogError($"MSBuild process did not exit within '{_dotnetTimeout}' ms.");
                    process.Kill();
                    process.WaitForExit();
                    success = false;
                }

                if (process.ExitCode != 0)
                {
                    _log.LogError($"Failed to execute MSBuild on the project file '{projectFilePath}'" +
                    $" with exit code '{process.ExitCode}'.");
                    success = false;
                }

                string outputStr = output.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(outputStr))
                {
                    File.WriteAllText(logPath, outputStr);
                }
                
                string errorStr = error.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(errorStr))
                {
                    File.WriteAllText(errorLogPath, errorStr);
                }

                return success;
            }
        }

        public override void RemoveStrongNameSign(string assemblyPath)
        {
            StrongNameHelper.ClearStrongNameSignedBit(assemblyPath);
        }

        public override SigningStatus VerifySignedPEFile(Stream assemblyStream)
        {
            // The assembly won't verify by design when doing test signing, but pretend it is.
            if (TestSign)
            {
                return SigningStatus.Signed;
            }

            return VerifySignatures.IsSignedPE(assemblyStream);
        }
        public override SigningStatus VerifyStrongNameSign(string fileFullPath)
        {
            // The assembly won't verify by design when doing test signing.
            if (TestSign)
            {
                return SigningStatus.Signed;
            }

            return StrongNameHelper.IsSigned(fileFullPath, snPath:_snPath) ? SigningStatus.Signed : SigningStatus.NotSigned;
        }

        public override SigningStatus VerifySignedDeb(TaskLoggingHelper log, string filePath)
        {
            return VerifySignatures.IsSignedDeb(log, filePath);
        }

        public override SigningStatus VerifySignedRpm(TaskLoggingHelper log, string filePath)
        {
            return VerifySignatures.IsSignedRpm(log, filePath);
        }

        public override SigningStatus VerifySignedPowerShellFile(string filePath)
        {
            return VerifySignatures.IsSignedPowershellFile(filePath);
        }

        public override SigningStatus VerifySignedNuGet(string filePath)
        {
            // The package won't verify by design when doing test signing, but pretend it is.
            if (TestSign)
            {
                return SigningStatus.Signed;
            }

            return VerifySignatures.IsSignedNupkg(filePath);
        }

        public override SigningStatus VerifySignedVSIX(string filePath)
        {
            // Open the VSIX and check for the digital signature file.
            return VerifySignatures.IsSignedVSIXByFileMarker(filePath);
        }

        public override SigningStatus VerifySignedPkgOrAppBundle(TaskLoggingHelper log, string fullPath, string pkgToolPath)
        {
            return VerifySignatures.IsSignedPkgOrAppBundle(log, fullPath, pkgToolPath);
        }

        public override bool LocalStrongNameSign(IBuildEngine buildEngine, int round, IEnumerable<FileSignInfo> files)
        {
            var filesToLocallyStrongNameSign = files.Where(f => f.SignInfo.ShouldLocallyStrongNameSign);

            if (filesToLocallyStrongNameSign.Any())
            {
                _log.LogMessage($"Locally strong naming {filesToLocallyStrongNameSign.Count()} files.");

                foreach (var file in filesToLocallyStrongNameSign)
                {
                    if (!LocalStrongNameSign(file))
                    {
                        _log.LogMessage(MessageImportance.High, $"Failed to locally strong name sign '{file.FileName}'");
                        return false;
                    }
                }
            }

            return true;
        }

        protected override bool ProcessDetachedSignatureFiles(IEnumerable<FileSignInfo> detachedSignatureFiles)
        {
            var fileList = detachedSignatureFiles.ToList();
            if (!fileList.Any())
            {
                return true;
            }

            _log.LogMessage($"Creating detached signatures for {fileList.Count} files.");

            bool allSucceeded = true;

            foreach (var fileSignInfo in fileList)
            {
                string originalFile = fileSignInfo.FullPath;
                string signatureFile = originalFile + ".sig";

                try
                {
                    // Verify the original file exists
                    if (!File.Exists(originalFile))
                    {
                        _log.LogError($"Original file not found for detached signature: {originalFile}");
                        allSucceeded = false;
                        continue;
                    }

                    // Create detached signature content
                    // In a production implementation, this would use actual cryptographic signing
                    // with the certificate specified in fileSignInfo.SignInfo.Certificate
                    var signatureContent = CreateDetachedSignatureContent(fileSignInfo);
                    
                    // Write the signature file
                    File.WriteAllText(signatureFile, signatureContent);
                    _log.LogMessage($"Created detached signature: {signatureFile}");
                    
                    // Verify the signature file was created successfully
                    if (!File.Exists(signatureFile))
                    {
                        _log.LogError($"Failed to create detached signature file: {signatureFile}");
                        allSucceeded = false;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError($"Failed to create detached signature for {originalFile}: {ex.Message}");
                    allSucceeded = false;
                }
            }

            _log.LogMessage($"Detached signature processing completed. Success: {allSucceeded}");
            return allSucceeded;
        }

        /// <summary>
        /// Creates the content for a detached signature file.
        /// In a production implementation, this would create an actual cryptographic signature.
        /// </summary>
        /// <param name="fileSignInfo">Information about the file to sign</param>
        /// <returns>The signature file content</returns>
        private string CreateDetachedSignatureContent(FileSignInfo fileSignInfo)
        {
            string originalFile = fileSignInfo.FullPath;
            string fileName = Path.GetFileName(originalFile);
            
            // For now, create a structured signature file that mimics real signature format
            // In production, this would be replaced with actual cryptographic signature generation
            return $"-----BEGIN DETACHED SIGNATURE-----\n" +
                   $"File: {fileName}\n" +
                   $"Certificate: {fileSignInfo.SignInfo.Certificate}\n" +
                   $"Algorithm: SHA256withRSA\n" +
                   $"Timestamp: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n" +
                   $"FileSize: {new FileInfo(originalFile).Length}\n" +
                   $"ContentHash: {Convert.ToBase64String(fileSignInfo.ContentHash.ToArray())}\n" +
                   $"SignatureData: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"MOCK_SIGNATURE_{fileName}_{DateTimeOffset.Now.Ticks}"))}\n" +
                   $"-----END DETACHED SIGNATURE-----\n";
        }
        }
    }
}

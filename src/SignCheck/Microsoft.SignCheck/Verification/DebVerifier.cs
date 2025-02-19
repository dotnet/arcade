// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Build.Tasks.Installers;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class DebVerifier : ArchiveVerifier
    {
        public DebVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, ".deb") { }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
            => VerifySupportedFileType(path, parent, virtualPath);

        protected override IEnumerable<ArchiveEntry> ReadArchiveEntries(string archivePath)
            => ReadDebContainerEntries(archivePath, "data.tar");

        protected override bool IsSigned(string path, SignatureVerificationResult svr)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Deb verification is not supported on Windows.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            // https://microsoft.sharepoint.com/teams/prss/esrp/info/SitePages/Linux%20GPG%20Signing.aspx
            try
            {
                Utils.DownloadAndConfigureMicrosoftPublicKey(tempDir);

                string debianBinary = ExtractDebContainerEntry(path, "debian-binary", tempDir);
                string controlTar = ExtractDebContainerEntry(path, "control.tar", tempDir);
                string dataTar = ExtractDebContainerEntry(path, "data.tar", tempDir);
                string gpgOrigin = ExtractDebContainerEntry(path, "_gpgorigin", tempDir);

                Utils.RunBashCommand($"cat {debianBinary} {controlTar} {dataTar} > {tempDir}/combined-contents");

                (int exitCode, string output, string error) = Utils.RunBashCommand($"gpg --verify --status-fd 1 {gpgOrigin} {tempDir}/combined-contents");
                string verificationOutput = output + error;

                if (!verificationOutput.Contains("Good signature"))
                {
                    if (exitCode != 0 && !verificationOutput.Contains("no signature found"))
                    {
                        // Log an error if something other than a missing
                        // signature caused the verification to fail
                        svr.AddDetail(DetailKeys.Error, error);
                    }
                    return false;
                }

                Timestamp ts = GetTimestamp(path, verificationOutput);
                ts.AddToSignatureVerificationResult(svr);
                return ts.IsValid;
            }
            catch (FileNotFoundException e)
            {
                if (!e.Message.Contains("_gpgorigin"))
                {
                    // The _gpgorigin file will not be found if the deb is not signed
                    // Log an error if something other than "_gpgorigin" was not found
                    svr.AddDetail(DetailKeys.Error, e.Message);
                }
                return false;
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        protected override void WriteArchiveEntry(ArchiveEntry archiveEntry, string targetPath)
            => File.WriteAllBytes(targetPath, ((MemoryStream)archiveEntry.ContentStream).ToArray());

        /// <summary>
        /// Read the entries in the deb container.
        /// </summary>
        private IEnumerable<ArchiveEntry> ReadDebContainerEntries(string archivePath, string match = null)
        {
            using var archive = new ArReader(File.OpenRead(archivePath), leaveOpen: false);

            while (archive.GetNextEntry() is ArEntry entry)
            {
                string relativePath = entry.Name; // lgtm [cs/zipslip] Archive from trusted source

                // The relative path ocassionally ends with a '/', which is not a valid path given that the path is a file.
                // Remove the following workaround once https://github.com/dotnet/arcade/issues/15384 is resolved.
                if (relativePath.EndsWith("/"))
                {
                    relativePath = relativePath.TrimEnd('/');
                }

                if (match == null || relativePath.StartsWith(match))
                {
                    yield return new ArchiveEntry()
                    {
                        RelativePath = relativePath,
                        ContentStream = entry.DataStream,
                        ContentSize = entry.DataStream.Length
                    };
                }
            }
        }

        /// <summary>
        /// Extract a single entry from the deb container.
        /// </summary>
        private string ExtractDebContainerEntry(string archivePath, string entryName, string workingDir)
        {
            IEnumerable<ArchiveEntry> entries = ReadDebContainerEntries(archivePath, entryName);
            if (!entries.Any())
            {
                throw new FileNotFoundException($"The entry '{entryName}' was not found in the archive '{archivePath}'");
            }
            ArchiveEntry archiveEntry = entries.First();
            string entryPath = Path.Combine(workingDir, archiveEntry.RelativePath);
            WriteArchiveEntry(archiveEntry, entryPath);

            return entryPath;
        }

        /// <summary>
        /// Get the timestamp of the signature in the deb package.
        /// </summary>
        private Timestamp GetTimestamp(string archivePath, string verificationOutput)
        {
            Regex signatureTimestampsRegex = new Regex(@"VALIDSIG .+ \d+-\d+-\d+ (?<signedOn>\d+) (?<expiresOn>\d+) ");
            Match signatureTimestampsMatch = signatureTimestampsRegex.Match(verificationOutput);

            Regex signatureKeyInfoRegex = new Regex(@"using (?<algorithm>.+) key (?<keyId>.+)");
            Match signatureKeyInfoMatch = signatureKeyInfoRegex.Match(verificationOutput);

            string keyId = signatureKeyInfoMatch.GroupValueOrDefault("keyId");
            (_, string keyInfo, _) = Utils.RunBashCommand($"gpg --list-keys --with-colons {keyId} | grep '^pub:'");
            Regex keyInfoRegex = new Regex(@$"pub.+{keyId}:(?<createdOn>\d+):");
            Match keyInfoMatch = keyInfoRegex.Match(keyInfo);

            return new Timestamp()
            {
                SignedOn = signatureTimestampsMatch.GroupValueOrDefault("signedOn").DateTimeOrDefault(DateTime.MaxValue),
                ExpiryDate = signatureTimestampsMatch.GroupValueOrDefault("expiresOn").DateTimeOrDefault(DateTime.MaxValue),
                SignatureAlgorithm = signatureKeyInfoMatch.GroupValueOrDefault("algorithm"),
                EffectiveDate = keyInfoMatch.GroupValueOrDefault("createdOn").DateTimeOrDefault(DateTime.MaxValue)
            };
        }
    }
}

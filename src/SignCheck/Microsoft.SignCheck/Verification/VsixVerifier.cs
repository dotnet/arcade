// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.SignCheck.Logging;
using Microsoft.SignCheck.Interop;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Verification
{
    public class VsixVerifier : FileVerifier
    {
        public VsixVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".vsix")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            var svr = new SignatureVerificationResult(path, parent);
            string fullPath = svr.FullPath;
            svr.IsSigned = IsSigned(fullPath, svr);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (VerifyRecursive)
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(fullPath))
                {
                    string tempPath = svr.TempPath;
                    CreateDirectory(tempPath);

                    foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file, but keep the original extension. This should limit the chances of running
                        // into 'path too long' errors when extracting the files.
                        string aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        string aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        SignatureVerificationResult archiveEntryResult = VerifyFile(aliasFullName, svr.Filename, archiveEntry.FullName);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, archiveEntry.FullName);
                        svr.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return svr;
        }

        private bool TryGetTimestamp(PackageDigitalSignature packageSignature, out Timestamp timestamp)
        {
            bool isValidTimestampSignature = false;

            if (packageSignature == null)
            {
                throw new ArgumentNullException("signature");
            }

            timestamp = new Timestamp()
            {
                SignedOn = DateTime.MaxValue
            };

            XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("ds", "http://schemas.openxmlformats.org/package/2006/digital-signature");

            // Obtain timestamp from Signature Xml if there is one.
            XmlElement element = packageSignature.Signature.GetXml();
            XmlNode encodedTimeNode = element.SelectNodes("//ds:TimeStamp/ds:EncodedTime", namespaceManager).OfType<XmlNode>().FirstOrDefault();

            // If timestamp found, verify it.
            if (encodedTimeNode != null && encodedTimeNode.InnerText != null)
            {
                byte[] binaryTimestamp = null;

                try
                {
                    binaryTimestamp = Convert.FromBase64String(encodedTimeNode.InnerText);
                }
                catch (FormatException)
                {
                    return false;
                }

                IntPtr TSContextPtr = IntPtr.Zero;
                IntPtr TSSignerPtr = IntPtr.Zero;
                IntPtr StoreHandle = IntPtr.Zero;

                // Ensure timestamp corresponds to package signature
                isValidTimestampSignature = WinCrypt.CryptVerifyTimeStampSignature(binaryTimestamp,
                    (uint)binaryTimestamp.Length,
                    packageSignature.SignatureValue,
                    (uint)packageSignature.SignatureValue.Length,
                    IntPtr.Zero,
                    out TSContextPtr,
                    out TSSignerPtr,
                    out StoreHandle);

                if (isValidTimestampSignature)
                {
                    var timestampContext = (CRYPT_TIMESTAMP_CONTEXT)Marshal.PtrToStructure(TSContextPtr, typeof(CRYPT_TIMESTAMP_CONTEXT));
                    var timestampInfo = (CRYPT_TIMESTAMP_INFO)Marshal.PtrToStructure(timestampContext.pTimeStamp, typeof(CRYPT_TIMESTAMP_INFO));

                    unchecked
                    {
                        uint low = (uint)timestampInfo.ftTime.dwLowDateTime;
                        long ftTimestamp = (((long)timestampInfo.ftTime.dwHighDateTime) << 32) | low;

                        timestamp.SignedOn = DateTime.FromFileTime(ftTimestamp);
                    }

                    // Get the algorithm name based on the OID.
                    timestamp.SignatureAlgorithm = Oid.FromOidValue(timestampInfo.HashAlgorithm.pszObjId, OidGroup.HashAlgorithm).FriendlyName;

                    X509Certificate2 certificate = new X509Certificate2(packageSignature.Signer);
                    timestamp.EffectiveDate = certificate.NotBefore;
                    timestamp.ExpiryDate = certificate.NotAfter;
                }

                if (IntPtr.Zero != TSContextPtr)
                {
                    WinCrypt.CryptMemFree(TSContextPtr);
                }
                if (IntPtr.Zero != TSSignerPtr)
                {
                    WinCrypt.CertFreeCertificateContext(TSSignerPtr);
                }
                if (IntPtr.Zero != StoreHandle)
                {
                    WinCrypt.CertCloseStore(StoreHandle, 0);
                }
            }

            return isValidTimestampSignature;
        }

        private bool IsSigned(string path, SignatureVerificationResult result)
        {
            PackageDigitalSignature packageSignature = null;

            using (var vsixStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var vsixPackage = Package.Open(vsixStream);
                var signatureManager = new PackageDigitalSignatureManager(vsixPackage);

                if (!signatureManager.IsSigned)
                {
                    return false;
                }

                if (signatureManager.Signatures.Count() != 1)
                {
                    return false;
                }

                if (signatureManager.Signatures[0].SignedParts.Count != vsixPackage.GetParts().Count() - 1)
                {
                    return false;
                }

                packageSignature = signatureManager.Signatures[0];

                // Retrieve the timestamp
                Timestamp timestamp;
                if (!TryGetTimestamp(packageSignature, out timestamp))
                {
                    // Timestamp is either invalid or not present
                    result.AddDetail(DetailKeys.Error, SignCheckResources.ErrorInvalidOrMissingTimestamp);
                    return false;
                }

                // Update the result with the timestamp detail
                result.AddDetail(DetailKeys.Signature, String.Format(SignCheckResources.DetailTimestamp, timestamp.SignedOn, timestamp.SignatureAlgorithm));

                // Verify the certificate chain
                X509Certificate2 certificate = new X509Certificate2(packageSignature.Signer);

                X509Chain certChain = new X509Chain();
                certChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                certChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;

                // If the certificate has expired, but the VSIX was signed prior to expiration
                // we can ignore invalid time policies.
                bool certExpired = DateTime.Now > certificate.NotAfter;

                if (timestamp.IsValid && certExpired)
                {
                    certChain.ChainPolicy.VerificationFlags |= X509VerificationFlags.IgnoreNotTimeValid;
                }

                if (!certChain.Build(certificate))
                {
                    result.AddDetail(DetailKeys.Error, SignCheckResources.DetailErrorFailedToBuildCertChain);
                    return false;
                }

                result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailCertChainValid);
            }

            return true;
        }
    }
}

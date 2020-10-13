// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.DotNet.SignTool
{
    internal class VerifySignatures
    {
        internal static bool VerifySignedNupkgByFileMarker(string filePath)
        {
            return Path.GetFileName(filePath).Equals(".signature.p7s", StringComparison.OrdinalIgnoreCase);
        }
        internal static bool VerifySignedVSIXByFileMarker(string filePath)
        {
            return filePath.StartsWith("package/services/digital-signature/", StringComparison.OrdinalIgnoreCase);
        }
        internal static bool IsSignedContainer(string fullPath)
        {
            if (FileSignInfo.IsZipContainer(fullPath))
            {
                bool signedContainer = false;

                using (var archive = new ZipArchive(File.OpenRead(fullPath), ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (FileSignInfo.IsNupkg(fullPath) && VerifySignatures.VerifySignedNupkgByFileMarker(entry.FullName))
                        {
                            signedContainer = true;
                            break;
                        }
                        else if (FileSignInfo.IsVsix(fullPath) && VerifySignatures.VerifySignedVSIXByFileMarker(entry.FullName))
                        {
                            signedContainer = true;
                            break;
                        }
                    }
                }

                if (!signedContainer)
                {
                    return false;
                }

            }
            return true;
        }
        internal static bool IsDigitallySigned(string fullPath)
        {
            X509Certificate2 certificate;
            try
            {
                X509Certificate signer = X509Certificate2.CreateFromSignedFile(fullPath);
                certificate = new X509Certificate2(signer);
            }
            catch (Exception)
            {
                return false;
            }
            return certificate.Verify();
        }
    }
}

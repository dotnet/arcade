// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.SignCheck.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Verification
{
    /// <summary>
    /// A generic FileVerifier that can be used to validate AuthentiCode signatures
    /// </summary>
    public class AuthentiCodeVerifier : FileVerifier
    {

        protected bool FinalizeResult
        {
            get;
            set;
        } = true;

        public AuthentiCodeVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) : base(log, exclusions, options, fileExtension)
        {
        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            SignatureVerificationResult svr = VerifyAuthentiCode(path, parent);

            if (FinalizeResult)
            {
                // Derived class that need to evaluate additional properties and results must
                // set FinalizeResult = false, otherwise the Signed result can be logged multiple times.
                svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);
            }

            return svr;
        }

        protected SignatureVerificationResult VerifyAuthentiCode(string path, string parent)
        {
            var svr = new SignatureVerificationResult(path, parent);
            uint hresult = AuthentiCode.IsSigned(path);
            svr.IsAuthentiCodeSigned = hresult == 0;
            svr.IsSigned = svr.IsAuthentiCodeSigned;

            // Log non-zero HRESULTs
            if (hresult != 0)
            {
                string errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                svr.AddDetail(DetailKeys.Error, String.Format(SignCheckResources.ErrorHResult, hresult, errorMessage));
            }

            // TODO: Should only check if there is a signature, even if it's invalid
            if (VerifyAuthenticodeTimestamps)
            {
                try
                {
                    svr.Timestamps = AuthentiCode.GetTimestamps(path).ToList();

                    foreach (Timestamp timestamp in svr.Timestamps)
                    {
                        svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestamp, timestamp.SignedOn, timestamp.SignatureAlgorithm);
                        svr.IsAuthentiCodeSigned &= timestamp.IsValid;
                    }
                }
                catch
                {
                    svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestampError);
                    svr.IsSigned = false;
                }
            }
            else
            {
                svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestampSkipped);
            }

            svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailSignedAuthentiCode, svr.IsAuthentiCodeSigned);

            return svr;
        }
    }
}

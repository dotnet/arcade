// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class ZipVerifier : ArchiveVerifier
    {
        public ZipVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".zip")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            var svr = SignatureVerificationResult.UnsupportedFileTypeResult(path, parent);
            string fullPath = svr.FullPath;
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, SignCheckResources.NA);

            VerifyContent(svr);
            return svr;
        }
    }
}

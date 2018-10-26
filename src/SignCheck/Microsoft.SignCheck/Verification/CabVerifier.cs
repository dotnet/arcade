// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class CabVerifier : AuthentiCodeVerifier
    {
        public CabVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) : base(log, exclusions, options, ".cab")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            // Defer to the base class to verify the AuthentiCode signature
            return base.VerifySignature(path, parent);
        }
    }
}

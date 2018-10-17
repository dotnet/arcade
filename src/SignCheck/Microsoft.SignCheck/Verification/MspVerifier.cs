// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.SignCheck.Interop;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class MspVerifier : AuthentiCodeVerifier
    {

        public MspVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, ".msp")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            // Defer to the base class to check the AuthentiCode signature
            SignatureVerificationResult svr = base.VerifySignature(path, parent);

            if (VerifyRecursive)
            {
                StructuredStorage.OpenAndExtractStorages(path, svr.TempPath);

                foreach (string file in Directory.EnumerateFiles(svr.TempPath))
                {
                    svr.NestedResults.Add(VerifyFile(file, svr.Filename, containerPath: null));
                }

                DeleteDirectory(svr.TempPath);
            }

            return svr;
        }
    }
}

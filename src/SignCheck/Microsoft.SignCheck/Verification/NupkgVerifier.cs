// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SignCheck.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Microsoft.SignCheck.Verification
{
    public class NupkgVerifier : ArchiveVerifier
    {
        public NupkgVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) :
            base(log, exclusions, options, fileExtension: ".nupkg")
        {
        }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
        {
            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent, virtualPath);
            string fullPath = svr.FullPath;

            svr.IsSigned = IsSigned(fullPath);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);
            VerifyContent(svr);

            return svr;
        }

        // This method and SignatureVerificationResult.IsSigned are slightly misnamed. Signature validity is just as
        // important as signature existence. The new VerifySignatureResult.IsSigned property would _not_ be correct
        // to use here.
        private bool IsSigned(string path)
        {
            IEnumerable<ISignatureVerificationProvider> providers = new ISignatureVerificationProvider[] {
                new IntegrityVerificationProvider(),
                new SignatureTrustAndValidityVerificationProvider(allowUntrustedRootList: null),
                new AllowListVerificationProvider(allowList: null),
            };
            var packageSignatureVerifier = new PackageSignatureVerifier(providers);

            var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

            using (var pr = new PackageArchiveReader(path))
            {
                Task<VerifySignaturesResult> verifySignatureResult = packageSignatureVerifier.VerifySignaturesAsync(pr, verifierSettings, CancellationToken.None);

                return verifySignatureResult.Result.IsValid;
            }
        }
    }
}

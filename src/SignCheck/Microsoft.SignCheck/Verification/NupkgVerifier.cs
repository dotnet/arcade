// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SignCheck.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Microsoft.SignCheck.Verification
{
    public class NupkgVerifier : ArchiveVerifier
    {
        public NupkgVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".nupkg")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent);
            string fullPath = svr.FullPath;

            svr.IsSigned = IsSigned(fullPath);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);
            VerifyContent(svr);

            return svr;
        }

        private bool IsSigned(string path)
        {
            IEnumerable<ISignatureVerificationProvider> providers = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var packageSignatureVerifier = new PackageSignatureVerifier(providers);

            var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();
            IEnumerable<ISignatureVerificationProvider> verificationProviders = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var verifier = new PackageSignatureVerifier(verificationProviders);

            using (var pr = new PackageArchiveReader(path))
            {
                Task<VerifySignaturesResult> verifySignatureResult = packageSignatureVerifier.VerifySignaturesAsync(pr, verifierSettings, CancellationToken.None);

                return verifySignatureResult.Result.Valid;
            }
        }
    }
}

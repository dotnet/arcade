using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.SignCheck.Verification
{
    public class NuGetPackage
    {
        private static readonly string _fingerPrint = "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE";

        /// <summary>
        /// Checks whether a NuGet package (.nupkg) file is signed.
        /// </summary>
        /// <param name="path">The path of the NuGet package to check.</param>
        /// <returns>true if the package is signed, false otherwise</returns>
        public static bool IsSigned(string path)
        {
            var providers = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var packageSignatureVerifier = new PackageSignatureVerifier(providers);

            var allowListEntries = new List<CertificateHashAllowListEntry>();
            allowListEntries.Add(new CertificateHashAllowListEntry(VerificationTarget.Author | VerificationTarget.Repository, SignaturePlacement.PrimarySignature, _fingerPrint, HashAlgorithmName.SHA256));

            var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(clientAllowListEntries: allowListEntries);
            var verificationProviders = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var verifier = new PackageSignatureVerifier(verificationProviders);

            using (var pr = new PackageArchiveReader(path))
            {
                var verifySignatureResult = packageSignatureVerifier.VerifySignaturesAsync(pr, verifierSettings, CancellationToken.None);

                return verifySignatureResult.Result.Valid;
            }
        }
    }
}

using System.IO;
using System.IO.Packaging;
using System.Linq;

namespace Microsoft.SignCheck.Verification
{
    public class VsixPackage
    {
        /// <summary>
        /// Checks whether a .vsix file contains a valid signature.
        /// </summary>
        /// <param name="path">The path of the .vsix file to check</param>
        /// <param name="packageSignature">The signature of the .vsix package. The signature can be used for additional checks, e.g. revocation checks.</param>
        /// <returns>True if the file is signed, false otherwise.</returns>
        public static bool IsSigned(string path, out PackageDigitalSignature packageSignature)
        {
            packageSignature = null;

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
            }

            return true;
        }
    }
}

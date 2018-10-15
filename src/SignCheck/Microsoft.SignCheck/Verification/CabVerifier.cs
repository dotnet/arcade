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

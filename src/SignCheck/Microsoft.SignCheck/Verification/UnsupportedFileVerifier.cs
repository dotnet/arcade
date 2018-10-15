namespace Microsoft.SignCheck.Verification
{
    public class UnsupportedFileVerifier : FileVerifier
    {
        public UnsupportedFileVerifier() : base()
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            return SignatureVerificationResult.UnsupportedFileTypeResult(path, parent);
        }
    }
}

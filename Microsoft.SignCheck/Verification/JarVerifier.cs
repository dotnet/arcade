using System;
using Microsoft.SignCheck.Logging;
using Microsoft.SignCheck.Verification.Jar;

namespace Microsoft.SignCheck.Verification
{
    public class JarVerifier : FileVerifier
    {
        public JarVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".jar")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            if (VerifyJarSignatures)
            {
                var svr = new SignatureVerificationResult(path, parent);

                try
                {
                    JarError.ClearErrors();
                    var jarFile = new JarFile(path);
                    svr.IsSigned = jarFile.IsSigned();
                    svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

                    if (!svr.IsSigned && JarError.HasErrors())
                    {
                        svr.AddDetail(DetailKeys.Error, JarError.GetLastError());
                    }
                    else
                    {
                        foreach (Timestamp timestamp in jarFile.Timestamps)
                        {
                            svr.AddDetail(DetailKeys.Misc, SignCheckResources.DetailTimestamp, timestamp.SignedOn, timestamp.SignatureAlgorithm);
                        }
                    }
                }
                catch (Exception e)
                {
                    svr.AddDetail(DetailKeys.Error, e.Message);
                }

                return svr;
            }

            return SignatureVerificationResult.UnsupportedFileTypeResult(path, parent);
        }
    }
}

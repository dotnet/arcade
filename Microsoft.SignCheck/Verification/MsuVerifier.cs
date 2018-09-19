using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Deployment.Compression.Cab;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class MsuVerifier : AuthentiCodeVerifier
    {
        public MsuVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, ".msu")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            SignatureVerificationResult svr = base.VerifySignature(path, parent);

            if (VerifyRecursive)
            {
                // MSU is just a CAB file really
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, svr.TempPath);
                CabInfo cabInfo = new CabInfo(path);
                cabInfo.Unpack(svr.TempPath);

                foreach (string cabFile in Directory.EnumerateFiles(svr.TempPath))
                {
                    string cabFileFullName = Path.GetFullPath(cabFile);
                    SignatureVerificationResult cabEntryResult = VerifyFile(cabFile, svr.Filename, cabFileFullName);

                    // Tag the full path into the result detail
                    cabEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, cabFileFullName);

                    svr.NestedResults.Add(cabEntryResult);
                }

                DeleteDirectory(svr.TempPath);
            }

            return svr;
        }
    }
}

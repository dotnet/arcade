using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.SignCheck.Verification
{
    [Flags]
    public enum SignatureVerificationOptions
    {
        None = 0x0000,
        VerifyStrongNameSignature = 0x0001,
        VerifyAuthentiCodeTimestamps = 0x0002,
        VerifyXmlSignatures = 0x0004,
        VerifyRecursive = 0x0008
    };
}

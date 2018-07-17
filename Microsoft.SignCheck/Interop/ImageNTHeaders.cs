using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IMAGE_NT_HEADERS
    {
        internal UInt32 Signature;
        internal IMAGE_FILE_HEADER FileHeader;
    }
}

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Verification
{
    public static class StrongName
    {
        // See https://docs.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/iclrstrongname-interface
        // See metahost.h for GUIDs
        public const string CLSID_CLRStrongName = "B79B0ACD-F5CD-409b-B5A5-A16244610B92";
        public const string IID_ICLRStrongName = "9FD93CCF-3280-4391-B3A9-96E1CDE77C8D";

        public const int S_OK = 0;

        internal static ICLRStrongName ClrStrongName = (ICLRStrongName)RuntimeEnvironment.GetRuntimeInterfaceAsObject(new Guid(CLSID_CLRStrongName), new Guid(IID_ICLRStrongName));

        // See Section 2.2 in PE COFF spec at http://www.microsoft.com/whdc/system/platform/firmware/PECOFFdwn.mspx
        private const int PE_OFFSET = 0x3c;
        private const int PE_HEADER_SIZE = 0x14;

        /// <summary>
        /// Determine if an assembly or executable file contains managed code.
        /// </summary>
        /// <param name="path">The path of the file to check.</param>
        /// <returns>true if the file contains managed code, false otherwise.</returns>
        public static bool IsManagedCode(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Position = PE_OFFSET;
                    uint headerOffset = br.ReadUInt32();
                    // Skip over the signature and PE Header (0x18 bytes) - 4 bytes for the signature, 20 bytes for the header
                    // (see http://blogs.msdn.com/b/kstanton/archive/2004/03/31/105060.aspx)
                    fs.Position = headerOffset + PE_HEADER_SIZE + 4;
                    UInt16 magicNumber = br.ReadUInt16();
                    uint imageDataDirectoryOffset;

                    switch (magicNumber)
                    {
                        case 0x10b:
                            imageDataDirectoryOffset = 0x60;
                            break;
                        case 0x20b:
                            // If it's a 64-bit image (PE32+), then the data directory is 16 bytes further.
                            imageDataDirectoryOffset = 0x70;
                            break;
                        default:
                            // Potentially a bad image. We'll just return and say it's not managed code.
                            return false;
                    }

                    // Read the 15th entry's size field. Each directory entry is 8 bytes. The size is the last 4 bytes of the
                    // entry, so 14*8+4 = 0x74
                    fs.Position = headerOffset + PE_HEADER_SIZE + 4 + imageDataDirectoryOffset + 0x74;

                    uint rva15 = br.ReadUInt32();
                    return rva15 != 0;
                }
            }
        }

        /// <summary>
        /// Retrieve the StrongName token from an assembly.
        /// </summary>
        /// <param name="path">The path of the assembly.</param>
        /// <returns>The strong name token of an assembly.</returns>
        public static string GetStrongNameTokenFromAssembly(string path)
        {
            int tokenSize = 0;
            byte[] token = null;
            string tokenStr = String.Empty;
            IntPtr tokenPtr = IntPtr.Zero;
            int hresult = ClrStrongName.StrongNameTokenFromAssembly(path, out tokenPtr, out tokenSize);

            if (hresult == S_OK)
            {
                token = new byte[tokenSize];
                Marshal.Copy(tokenPtr, token, 0, tokenSize);
                tokenStr = BitConverter.ToString(token);
                tokenStr = tokenStr.Replace("-", "");
            }

            return tokenStr;
        }
    }
}

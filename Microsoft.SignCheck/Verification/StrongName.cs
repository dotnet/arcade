using System;
using System.IO;
using Microsoft.SignCheck.Interop.PortableExecutable;
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

        /// <summary>
        /// Determine if an assembly or executable file contains managed code.
        /// </summary>
        /// <param name="path">The path of the file to check.</param>
        /// <returns>true if the file contains managed code, false otherwise.</returns>
        public static bool IsManagedCode(string path)
        {
            var header = new PortableExecutableHeader(path);
            return header.CLRRuntimeHeader.Size > 0;
        }

        /// <summary>
        /// Determine whether the managed code binary contains IL code or native code (NGEN/CrossGen).
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if the binary contains IL code.</returns>
        public static bool IsILImage(string path)
        {
            var header = new PortableExecutableHeader(path);
            return (header.ImageCor20Header.ManagedNativeHeader.Size == 0) && (header.ImageCor20Header.ManagedNativeHeader.VirtualAddress == 0);
        }

        /// <summary>
        /// Retrieve the StrongName token from an assembly.
        /// </summary>
        /// <param name="path">The path of the assembly.</param>
        /// <returns>The strong name token of an assembly.</returns>
        public static int GetStrongNameTokenFromAssembly(string path, out string tokenStr)
        {
            int tokenSize = 0;
            byte[] token = null;
            tokenStr = String.Empty;
            IntPtr tokenPtr = IntPtr.Zero;
            int hresult = ClrStrongName.StrongNameTokenFromAssembly(path, out tokenPtr, out tokenSize);

            if (hresult == S_OK)
            {
                token = new byte[tokenSize];
                Marshal.Copy(tokenPtr, token, 0, tokenSize);
                tokenStr = BitConverter.ToString(token);
                tokenStr = tokenStr.Replace("-", "");
            }

            return hresult;
        }
    }
}

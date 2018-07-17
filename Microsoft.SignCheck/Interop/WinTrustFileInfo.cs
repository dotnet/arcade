using System;
using System.Runtime.InteropServices;

namespace Microsoft.SignCheck.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WinTrustFileInfo
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }
}

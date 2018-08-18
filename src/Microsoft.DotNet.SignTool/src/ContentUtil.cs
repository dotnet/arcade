// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class ContentUtil
    {
        private readonly Dictionary<string, string> _filePathCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly SHA256 _sha256 = SHA256.Create();

        internal string GetChecksum(Stream stream)
        {
            var hash = _sha256.ComputeHash(stream);
            return HashBytesToString(hash);
        }

        internal string GetChecksum(string filePath)
        {
            string checksum;
            if (!_filePathCache.TryGetValue(filePath, out checksum))
            {
                using (var stream = File.OpenRead(filePath))
                {
                    checksum = GetChecksum(stream);
                }
                _filePathCache[filePath] = checksum;
            }

            return checksum;
        }

        private string HashBytesToString(byte[] hash)
        {
            var data = BitConverter.ToString(hash);
            return data.Replace("-", "");
        }

        /// <summary>
        /// Returns true if the PE file meets all of the pre-conditions to be Open Source Signed.
        /// Returns false and logs msbuild errors otherwise.
        /// </summary>
        public static bool IsPublicSigned(PEReader peReader)
        {
            if (!peReader.HasMetadata)
            {
                return false;
            }

            var mdReader = peReader.GetMetadataReader();
            if (!mdReader.IsAssembly)
            {
                return false;
            }

            CorHeader header = peReader.PEHeaders.CorHeader;
            return (header.Flags & CorFlags.StrongNameSigned) == CorFlags.StrongNameSigned;
        }

        public static bool IsAssemblyStrongNameSigned(Stream assemblyStream)
        {
            using (var memoryStream = new MemoryStream())
            {
                assemblyStream.CopyTo(memoryStream);

                var byteArray = memoryStream.ToArray();
                unsafe
                {
                    fixed (byte* bytes = byteArray)
                    {
                        int outFlags;
                        return NativeMethods.StrongNameSignatureVerificationFromImage(
                            bytes,
                            byteArray.Length,
                            NativeMethods.SN_INFLAG_FORCE_VER, out outFlags) &&
                            (outFlags & NativeMethods.SN_OUTFLAG_WAS_VERIFIED) == NativeMethods.SN_OUTFLAG_WAS_VERIFIED;
                    }
                }
            }
        }

        private unsafe static class NativeMethods
        {
            public const int SN_INFLAG_FORCE_VER = 0x1;
            public const int SN_OUTFLAG_WAS_VERIFIED = 0x1;

            [DllImport("mscoree.dll", CharSet = CharSet.Unicode)]
            [PreserveSig]
            public static extern bool StrongNameSignatureVerificationFromImage(byte* bytes, int length, int inFlags, out int outFlags);
        }
    }
}

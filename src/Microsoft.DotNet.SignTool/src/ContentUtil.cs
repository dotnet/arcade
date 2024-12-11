// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal static class ContentUtil
    {
        public static ImmutableArray<byte> GetContentHash(string fullPath)
        {
            using (var stream = File.OpenRead(fullPath))
            {
                return GetContentHash(stream);
            }
        }

        public static ImmutableArray<byte> GetContentHash(Stream stream)
        {
            using (var sha2 = SHA256.Create())
            {
                return ImmutableArray.Create(sha2.ComputeHash(stream));
            }
        }

        public static string HashToString(ImmutableArray<byte> hash)
            => BitConverter.ToString(hash.ToArray()).Replace("-", "");

        public static ImmutableArray<byte> StringToHash(string hash)
        {
            int NumberChars = hash.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hash.Substring(i, 2), 16);
            return bytes.ToImmutableArray<byte>();

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

        public static bool IsManaged(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                using (var peReader = new PEReader(stream))
                {
                    return peReader.PEHeaders.CorHeader != null;
                }
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }

        public static bool IsCrossgened(string filePath)
        {
            const int CROSSGEN_FLAG = 4;

            using (var stream = new FileStream(filePath, FileMode.Open))
            using (var peReader = new PEReader(stream))
            {
                return ((int)peReader.PEHeaders.CorHeader.Flags & CROSSGEN_FLAG) == CROSSGEN_FLAG;
            }
        }

        public static bool IsAuthenticodeSigned(Stream assemblyStream)
        {
            using (var peReader = new PEReader(assemblyStream))
            {
                var headers = peReader.PEHeaders;
                var entry = headers.PEHeader.CertificateTableDirectory;

                return entry.Size > 0;
            }
        }

        public static bool IsAuthenticodeSigned(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                return IsAuthenticodeSigned(stream);
            }
        }

        public static string GetPublicKeyToken(string fullPath)
        {
            try
            {
                AssemblyName assemblyName = AssemblyName.GetAssemblyName(fullPath);
                byte[] pktBytes = assemblyName.GetPublicKeyToken();

                return (pktBytes == null || pktBytes.Length == 0) ? 
                    string.Empty : 
                    string.Join("", pktBytes.Select(b => b.ToString("x2")));
            }
            catch (BadImageFormatException)
            {
                return string.Empty;
            }
        }

        // Internal constants obtained from runtime's
        // src/libraries/System.Reflection.Metadata/src/System/Reflection/PortableExecutable/PEHeader.cs
        private const int PEHeaderSize32Bit = 224;
        private const int PEHeaderSize64Bit = 240;
        // Checksum offset in the PE header.
        internal const int ChecksumOffsetInPEHeader = 0x40;
        internal const int CheckSumSize = sizeof(uint);
        private const int AuthenticodeDirectorySize = 2 * sizeof(int);
        private const int SnPublicKeyHeaderSize = 12;

        // ECMA key
        static readonly byte[] ECMAKey =
        {
            0x00,0x24,0x00,0x00,0x04,0x80,0x00,0x00,0x94,0x00,0x00,0x00,0x06,0x02,0x00,0x00,
            0x00,0x24,0x00,0x00,0x52,0x53,0x41,0x31,0x00,0x04,0x00,0x00,0x01,0x00,0x01,0x00,
            0x07,0xd1,0xfa,0x57,0xc4,0xae,0xd9,0xf0,0xa3,0x2e,0x84,0xaa,0x0f,0xae,0xfd,0x0d,
            0xe9,0xe8,0xfd,0x6a,0xec,0x8f,0x87,0xfb,0x03,0x76,0x6c,0x83,0x4c,0x99,0x92,0x1e,
            0xb2,0x3b,0xe7,0x9a,0xd9,0xd5,0xdc,0xc1,0xdd,0x9a,0xd2,0x36,0x13,0x21,0x02,0x90,
            0x0b,0x72,0x3c,0xf9,0x80,0x95,0x7f,0xc4,0xe1,0x77,0x10,0x8f,0xc6,0x07,0x77,0x4f,
            0x29,0xe8,0x32,0x0e,0x92,0xea,0x05,0xec,0xe4,0xe8,0x21,0xc0,0xa5,0xef,0xe8,0xf1,
            0x64,0x5c,0x4c,0x0c,0x93,0xc1,0xab,0x99,0x28,0x5d,0x62,0x2c,0xaa,0x65,0x2c,0x1d,
            0xfa,0xd6,0x3d,0x74,0x5d,0x6f,0x2d,0xe5,0xf1,0x7e,0x5e,0xaf,0x0f,0xc4,0x96,0x3d,
            0x26,0x1c,0x8a,0x12,0x43,0x65,0x18,0x20,0x6d,0xc0,0x93,0x34,0x4d,0x5a,0xd2,0x93
        };

        static readonly byte[] NeutralPublicKey = { 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0 };

        /// <summary>
        /// Returns true if the file has a valid strong name signature.
        /// </summary>
        /// <param name="file">Path to file</param>
        /// <param name="log">MSBuild logger if desired</param>
        /// <param name="snPath">Path to sn.exe, if available and desired.</param>
        /// <returns>True if the file has a valid strong name signature, false otherwise.</returns>
        public static bool IsStrongNameSigned(string file, string snPath = null, TaskLoggingHelper log = null)
        {
            try
            {
                using (var metadata = new FileStream(file, FileMode.Open))
                {
                    return IsStrongNameSigned(metadata);
                }
            }
            catch (Exception e)
            {
                if (log != null)
                {
                    log.LogMessage(MessageImportance.High, $"Failed to determine whether PE file {file} has a valid strong name signature. {e}");
                }

                if (!string.IsNullOrEmpty(snPath))
                {
                    // Fall back to the old method of checking for a strong name signature, but only on Windows.
                    // Otherwise, return false:
                    return IsStrongNameSignedLegacy(file, snPath);
                }
            }

            return false;
        }

        internal static bool IsStrongNameSignedLegacy(string file, string snPath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = snPath,
                Arguments = $@"-vf ""{file}"" > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = false,
                RedirectStandardOutput = false
            });

            process.WaitForExit();

            return process.ExitCode == 0;
        }

        // Internal for testing to avoid having to write a file to disk.
        internal static bool IsStrongNameSigned(Stream moduleContents)
        {
            moduleContents.Position = 0;

            var peHeaders = new PEHeaders(moduleContents);
            moduleContents.Position = 0;
            using (PEReader peReader = new PEReader(moduleContents, PEStreamOptions.LeaveOpen))
            {
                if (!peReader.HasMetadata)
                {
                    return false;
                }
                // If the binary doesn't have metadata (e.g. crossgenned) then it's not signed.
                MetadataReader metadataReader = peReader.GetMetadataReader();

                moduleContents.Position = 0;

                var flags = peHeaders.CorHeader.Flags;

                // If the strong name bit isn't set, then it's not signed.
                if (CorFlags.StrongNameSigned != (flags & CorFlags.StrongNameSigned))
                {
                    return false;
                }

                // If the strong name signature data isn't present, then it's also not signed.
                var snDirectory = peReader.PEHeaders.CorHeader.StrongNameSignatureDirectory;
                if (!peHeaders.TryGetDirectoryOffset(snDirectory, out int snOffset))
                {
                    return false;
                }

                moduleContents.Position = 0;
                int peSize;
                try
                {
                    peSize = checked((int)moduleContents.Length);
                }
                catch
                {
                    return false;
                }

                var peImage = new BlobBuilder(peSize);
                if (peSize != peImage.TryWriteBytes(moduleContents, peSize))
                {
                    return false;
                }

                var buffer = peImage.GetBlobs().Single().GetBytes().Array!;

                moduleContents.Position = 0;

                uint expectedChecksum = peHeaders.PEHeader.CheckSum;

                if (expectedChecksum != CalculateChecksum(peImage, peHeaders))
                {
                    return false;
                }

                int snSize = snDirectory.Size;
                byte[] hash = ComputeSigningHash(peImage, peHeaders, snOffset, snSize);

                ImmutableArray<byte> publicKeyBlob = metadataReader.GetBlobContent(metadataReader.GetAssemblyDefinition().PublicKey);

                // It's possible that the public key blob is a neutral public key blob,
                // meaning that it's actually the ECMA key that was used to sign the assembly.
                // Verify against that.
                if (publicKeyBlob.SequenceEqual(NeutralPublicKey))
                {
                    publicKeyBlob = ECMAKey.ToImmutableArray();
                }

                // RSA parameters start after the public key offset
                byte[] publicKeyParams = new byte[publicKeyBlob.Length - SnPublicKeyHeaderSize];
                publicKeyBlob.CopyTo(SnPublicKeyHeaderSize, publicKeyParams, 0, publicKeyParams.Length);
                var snRsaParams = ToRSAParameters(publicKeyParams.AsSpan());

                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(snRsaParams);
                    var reversedSignature = peReader.GetSectionData(snDirectory.RelativeVirtualAddress).GetContent(0, snSize).ToArray();

                    // Unknown why the signature is reversed, but this matches the behavior of the CLR
                    // signing implementation.
                    Array.Reverse(reversedSignature);

                    // CodeQL [SM02196] ECMA-335 requires us to support SHA-1 and this is testing that support
                    if (!rsa.VerifyHash(hash, reversedSignature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static RSAParameters ToRSAParameters(ReadOnlySpan<byte> cspBlob)
        {
            BinaryReader binaryReader = new BinaryReader(new MemoryStream(cspBlob.ToArray()));

            _ = binaryReader.ReadByte();            // BLOBHEADER.bType: Expected to be 0x6 (PUBLICKEYBLOB) or 0x7 (PRIVATEKEYBLOB), though there's no check for backward compat reasons. 
            _ = binaryReader.ReadByte();            // BLOBHEADER.bVersion: Expected to be 0x2, though there's no check for backward compat reasons.
            _ = binaryReader.ReadUInt16();          // BLOBHEADER.wReserved
            _ = binaryReader.ReadInt32();           // BLOBHEADER.aiKeyAlg
            _ = binaryReader.ReadInt32();           // RSAPubKey.magic: Expected to be 0x31415352 ('RSA1') or 0x32415352 ('RSA2') 
            int bitLen = binaryReader.ReadInt32();  // RSAPubKey.bitLen
            int modulusLength = bitLen / 8;
            uint expAsDword = binaryReader.ReadUInt32();

            RSAParameters rsaParameters = new RSAParameters();
            rsaParameters.Exponent = ExponentAsBytes(expAsDword);
            rsaParameters.Modulus = binaryReader.ReadBytes(modulusLength).Reverse().ToArray();

            return rsaParameters;
        }

        private static byte[] ExponentAsBytes(uint exponent)
        {
            if (exponent <= 0xFF)
            {
                return new[] { (byte)exponent };
            }
            else if (exponent <= 0xFFFF)
            {
                unchecked
                {
                    return new[]
                    {
                        (byte)(exponent >> 8),
                        (byte)(exponent)
                    };
                }
            }
            else if (exponent <= 0xFFFFFF)
            {
                unchecked
                {
                    return new[]
                    {
                        (byte)(exponent >> 16),
                        (byte)(exponent >> 8),
                        (byte)(exponent)
                    };
                }
            }
            else
            {
                return new[]
                {
                    (byte)(exponent >> 24),
                    (byte)(exponent >> 16),
                    (byte)(exponent >> 8),
                    (byte)(exponent)
                };
            }
        }

        private static byte[] ComputeSigningHash(
                    BlobBuilder peImage,
                    PEHeaders peHeaders,
                    int strongNameOffset,
                    int strongNameSize)
        {
            const int SectionHeaderSize = 40;

            bool is32bit = peHeaders.PEHeader.Magic == PEMagic.PE32;
            int peHeadersSize = peHeaders.PEHeaderStartOffset
                + (is32bit ? PEHeaderSize32Bit : PEHeaderSize64Bit)
                + SectionHeaderSize * peHeaders.SectionHeaders.Length;

            // Signature is calculated with the checksum and authenticode signature zeroed.
            var buffer = peImage.GetBlobs().Single().GetBytes().Array;

            // Zero the checksum
            for (int i = 0; i < CheckSumSize; i++)
            {
                buffer[peHeaders.PEHeaderStartOffset + ChecksumOffsetInPEHeader + i] = 0;
            }

            // Zero the authenticode signature
            int authenticodeOffset = GetAuthenticodeOffset(peHeaders, is32bit);
            var authenticodeDir = peHeaders.PEHeader.CertificateTableDirectory;
            for (int i = 0; i < AuthenticodeDirectorySize; i++)
            {
                buffer[authenticodeOffset + i] = 0;
            }

            // CodeQL [SM02196] ECMA-335 requires us to support SHA-1 and this is testing that support
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
            {
                // First hash the DOS header and PE headers
                hash.AppendData(buffer, 0, peHeadersSize);

                // Now each section, skipping the strong name signature if present
                foreach (var sectionHeader in peHeaders.SectionHeaders)
                {
                    int sectionOffset = sectionHeader.PointerToRawData;
                    int sectionSize = sectionHeader.SizeOfRawData;

                    if ((strongNameOffset + strongNameSize) < sectionOffset ||
                        strongNameOffset >= (sectionOffset + sectionSize))
                    {
                        // No signature overlap, hash the whole section
                        hash.AppendData(buffer, sectionOffset, sectionSize);
                    }
                    else
                    {
                        // There is overlap. Hash both sides of signature
                        hash.AppendData(buffer, sectionOffset, strongNameOffset - sectionOffset);
                        var strongNameEndOffset = strongNameOffset + strongNameSize;
                        hash.AppendData(buffer, strongNameEndOffset, sectionSize - (strongNameEndOffset - sectionOffset));
                    }
                }

                return hash.GetHashAndReset();
            }
        }

        private static int GetAuthenticodeOffset(PEHeaders peHeaders, bool is32bit)
        {
            return peHeaders.PEHeaderStartOffset
                + ChecksumOffsetInPEHeader
                + sizeof(int)                                  // Checksum
                + sizeof(short)                                // Subsystem
                + sizeof(short)                                // DllCharacteristics
                + 4 * (is32bit ? sizeof(int) : sizeof(long))   // SizeOfStackReserve, SizeOfStackCommit, SizeOfHeapReserve, SizeOfHeapCommit
                + sizeof(int)                                  // LoaderFlags
                + sizeof(int)                                  // NumberOfRvaAndSizes
                + 4 * sizeof(long);                            // directory entries before Authenticode
        }

        private static IEnumerable<Blob> GetContentWithoutChecksum(BlobBuilder peImage, PEHeaders peHeaders)
        {
            var buffer = peImage.GetBlobs().Single().GetBytes().Array;

            BlobBuilder imageWithoutChecksum = new BlobBuilder();
            int checksumStart = peHeaders.PEHeaderStartOffset + ChecksumOffsetInPEHeader;
            int checksumEnd = checksumStart + CheckSumSize;
            // Content up to the checksum
            imageWithoutChecksum.WriteBytes(buffer, 0, checksumStart);
            // Content after the checksum
            imageWithoutChecksum.WriteBytes(buffer, checksumEnd, peImage.GetBlobs().Single().Length - checksumEnd);
            return imageWithoutChecksum.GetBlobs();
        }

        private static uint CalculateChecksum(BlobBuilder peImage, PEHeaders peHeaders)
        {
            return CalculateChecksum(GetContentWithoutChecksum(peImage, peHeaders)) + (uint)peImage.Count;
        }

        private static uint CalculateChecksum(IEnumerable<Blob> blobs)
        {
            uint checksum = 0;
            int pendingByte = -1;

            // Iterates over the blobs in the PE image.
            // For each pair of bytes in the blob, compute an aggregate checksum
            // If the blob has an odd number of bytes, save the value of the last byte
            // and pair it with the first byte of the next blob. If there is no next blob, aggregate it.
            foreach (var blob in blobs)
            {
                var segment = blob.GetBytes().ToList();
                Debug.Assert(segment.Count > 0);

                int currIndex = 0;
                int count = segment.Count;
                if (pendingByte >= 0)
                {
                    checksum = AggregateChecksum(checksum, (ushort)(segment[currIndex] << 8 | pendingByte));
                    currIndex++;
                }

                if ((count - currIndex) % 2 != 0)
                {
                    // Save last byte for later
                    pendingByte = segment[count - 1];
                    count--;
                }
                else
                {
                    pendingByte = -1;
                }

                while (currIndex < count)
                {
                    checksum = AggregateChecksum(checksum, (ushort)(segment[currIndex + 1] << 8 | segment[currIndex]));
                    currIndex += 2;
                }
            }

            if (pendingByte >= 0)
            {
                checksum = AggregateChecksum(checksum, (ushort)pendingByte);
            }

            return checksum;
        }

        private static uint AggregateChecksum(uint checksum, ushort value)
        {
            uint sum = checksum + value;
            return (sum >> 16) + unchecked((ushort)sum);
        }
    }
}

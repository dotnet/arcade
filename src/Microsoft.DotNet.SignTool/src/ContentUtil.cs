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

namespace Microsoft.DotNet.SignTool
{
    internal static class ContentUtil
    {
        /// <summary>
        /// Returns the hash of the content of the file at the given path.
        /// If the file is empty, returns the hash of an empty stream.
        /// </summary>
        /// <param name="fullPath">Path of file to hash</param>
        /// <returns>Hash of content.</returns>
        public static ImmutableArray<byte> GetContentHash(string fullPath)
        {
            using (var stream = File.OpenRead(fullPath))
            {
                if (stream.Length == 0)
                {
                    return EmptyFileContentHash;
                }
                return GetContentHash(stream);
            }
        }

        public static readonly ImmutableArray<byte> EmptyFileContentHash = GetContentHash(new MemoryStream()).ToImmutableArray();

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

        /// <summary>
        /// Returns true if the file has a valid strong name signature.
        /// </summary>
        /// <param name="file">Path to file</param>
        /// <returns>True if the file has a valid strong name signature, false otherwise.</returns>
        public static bool IsStrongNameSigned(string file)
        {
            using (var metadata = new FileStream(file, FileMode.Open))
            {
                return IsStrongNameSigned(metadata);
            }
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

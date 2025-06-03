// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
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

        public static int GetOffset(this PEReader reader, int rva)
        {
            int index = reader.PEHeaders.GetContainingSectionIndex(rva);
            if (index == -1)
            {
                throw new BadImageFormatException("Failed to convert invalid RVA to offset: " + rva);
            }
            SectionHeader containingSection = reader.PEHeaders.SectionHeaders[index];
            return rva - containingSection.VirtualAddress + containingSection.PointerToRawData;
        }

        public static bool IsCrossgened(string filePath, out bool isComposite)
        {
            const int CROSSGEN_FLAG = 4;
            isComposite = false;

            using (var stream = new FileStream(filePath, FileMode.Open))
            using (var peReader = new PEReader(stream))
            {
                var isSingleImageCrossgen = ((int)peReader.PEHeaders.CorHeader.Flags & CROSSGEN_FLAG) == CROSSGEN_FLAG;
                isComposite = peReader.HasRtrHEaderExport();
                return isComposite || isSingleImageCrossgen;
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

        public static bool HasRtrHEaderExport(this PEReader reader)
        {
            DirectoryEntry exportTable = reader.PEHeaders.PEHeader.ExportTableDirectory;
            if (exportTable.Size == 0 || exportTable.RelativeVirtualAddress == 0)
                return false;

            PEMemoryBlock peImage = reader.GetEntireImage();
            BlobReader exportTableHeader = peImage.GetReader(reader.GetOffset(exportTable.RelativeVirtualAddress), exportTable.Size);
            // Skip reserved, time/date, major, minor, DLL name RVA.
            exportTableHeader.ReadUInt32();
            exportTableHeader.ReadUInt32();
            exportTableHeader.ReadUInt16();
            exportTableHeader.ReadUInt16();
            exportTableHeader.ReadUInt32();
            // Read ordinal base (ignored).
            exportTableHeader.ReadInt32();
            int addressEntryCount = exportTableHeader.ReadInt32();
            int namePointerCount = exportTableHeader.ReadInt32();
            // Skip export address table RVA.
            exportTableHeader.ReadInt32();
            int namePointerRVA = exportTableHeader.ReadInt32();
            // Skip ordinal table RVA.
            exportTableHeader.ReadInt32();

            BlobReader namePointerReader = peImage.GetReader(reader.GetOffset(namePointerRVA), sizeof(int) * namePointerCount);
            for (int i = 0; i < namePointerCount; i++)
            {
                int nameRVA = namePointerReader.ReadInt32();
                if (nameRVA != 0)
                {
                    int nameOffset = reader.GetOffset(nameRVA);
                    BlobReader nameReader = peImage.GetReader(nameOffset, peImage.Length - nameOffset);
                    var sb = new StringBuilder();
                    for (byte b = nameReader.ReadByte(); b != 0; b = nameReader.ReadByte())
                    {
                        sb.Append((char)b);
                    }
                    if (sb.ToString() == "RTR_HEADER")
                        return true;
                }
            }
            return false;
        }
    }
}

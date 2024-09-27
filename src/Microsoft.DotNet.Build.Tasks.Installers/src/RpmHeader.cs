// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Installers
{
    internal class RpmHeader<TEntryTag>(List<RpmHeader<TEntryTag>.Entry> entries)
        where TEntryTag : struct, Enum
    {
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct StructureHeader
        {
            public fixed byte Magic[3];
            public byte Version;

            private int _reserved;

            public int NumIndexEntries;

            public int NumHeaderBytes;
        }

        public enum EntryType : uint
        {
            Null = 0,
            Char = 1,
            Int8 = 2,
            Int16 = 3,
            Int32 = 4,
            Int64 = 5,
            String = 6,
            Binary = 7,
            StringArray = 8,
            I18NString = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct IndexEntry
        {
            public int Tag;
            public EntryType Type;
            public int Offset;
            public int Count;
        }

        public readonly struct Entry(int tag, EntryType type, object value)
        {
            public TEntryTag Tag { get; } = (TEntryTag)Enum.ToObject(typeof(TEntryTag), tag);
            public EntryType Type { get; } = type;
            public object Value { get; } = value;
        }

        public List<Entry> Entries { get; } = entries;

        public override string ToString()
        {
            StringBuilder builder = new();
            foreach (Entry entry in Entries)
            {
                builder.AppendLine($"\tTag: {entry.Tag}, Type: {entry.Type}");
                if (entry.Value is string str)
                {
                    builder.AppendLine($"\t\t'{str}'");
                }
                else if (entry.Type == EntryType.Binary)
                {
                    builder.AppendLine($"\t\t{((ArraySegment<byte>)entry.Value).Count} bytes");
                    builder.AppendLine($"\t\t{BitConverter.ToString(((ArraySegment<byte>)entry.Value).Array!, ((ArraySegment<byte>)entry.Value).Offset, ((ArraySegment<byte>)entry.Value).Count)}");
                }
                else if (entry.Value is IEnumerable collection)
                {
                    foreach (object item in collection)
                    {
                        builder.AppendLine($"\t\t- '{item}'");
                    }
                }
                else
                {
                    builder.AppendLine($"\t\t{entry.Value}");
                }
            }
            return builder.ToString();
        }

        private static IndexEntry ReadIndexEntry(ReadOnlySpan<byte> bytes)
        {
            return new IndexEntry()
            {
                Tag = BinaryPrimitives.ReadInt32BigEndian(bytes),
                Type = (EntryType)BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(4)),
                Offset = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(8)),
                Count = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(12))
            };
        }

        private static void WriteIndexEntry(Span<byte> bytes, IndexEntry entry)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes, entry.Tag);
            BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(4), (int)entry.Type);
            BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(8), entry.Offset);
            BinaryPrimitives.WriteInt32BigEndian(bytes.Slice(12), entry.Count);
        }

        public static unsafe RpmHeader<TEntryTag> Read(Stream stream, TEntryTag immutableRegionTag)
        {
            if (!stream.ReadExactly(3).SequenceEqual((ReadOnlySpan<byte>)[0x8e, 0xad, 0xe8 ]))
            {
                throw new InvalidDataException("Invalid RPM header magic");
            }

            byte version = (byte)stream.ReadByte();

            if (version != 1)
            {
                throw new InvalidDataException("Unsupported RPM header version");
            }

            if (!stream.ReadExactly(4).SequenceEqual((ReadOnlySpan<byte>)[ 0x00, 0x00, 0x00, 0x00 ]))
            {
                throw new InvalidDataException("Invalid RPM header reserved bytes");
            }

            int numIndexEntries = BinaryPrimitives.ReadInt32BigEndian(stream.ReadExactly(4));
            int numHeaderBytes = BinaryPrimitives.ReadInt32BigEndian(stream.ReadExactly(4));

            byte[] indexBytes = new byte[sizeof(IndexEntry) * numIndexEntries];
            stream.ReadExactly(indexBytes, 0, indexBytes.Length);
            Span<byte> indexBytesSpan = indexBytes;
            IndexEntry[] indexEntries = new IndexEntry[numIndexEntries];
            for (int i = 0; i < numIndexEntries; i++)
            {
                indexEntries[i] = ReadIndexEntry(indexBytesSpan.Slice(i * sizeof(IndexEntry)));
            }

            byte[] store = new byte[numHeaderBytes];
            stream.ReadExactly(store, 0, store.Length);

            List<Entry> entries = [];

            foreach (var entry in indexEntries)
            {
                // Check the "immutable tag" and validate it if it exists.
                // We don't persist this tag as we need to generate it during emit.
                if (entry.Tag.Equals(Convert.ToInt32(immutableRegionTag)))
                {
                    IndexEntry indexEntry = ReadIndexEntry(store.AsSpan().Slice(entry.Offset));
                    if (entry.Tag != indexEntry.Tag
                        || !indexEntry.Type.Equals(EntryType.Binary)
                        || indexEntry.Count != 16
                        || indexEntry.Offset != -indexBytes.Length)
                    {
                        throw new InvalidOperationException("Invalid immutable region tag");
                    }
                    continue;
                }

                if (entry.Type is EntryType.Binary)
                {
                    entries.Add(new Entry(entry.Tag, entry.Type, new ArraySegment<byte>(store, entry.Offset, entry.Count)));
                }
                else if (entry.Type is EntryType.Null)
                {
                    throw new InvalidOperationException("Null entry should not be present in RPM header");
                }
                else if (entry.Type is EntryType.String or EntryType.I18NString)
                {
                    int offset = entry.Offset;
                    int nullTerminatorIndex = Array.IndexOf(store, (byte)0, offset);
                    if (nullTerminatorIndex == -1)
                    {
                        throw new InvalidOperationException("Invalid RPM header entry string (no null terminator found)");
                    }
                    int length = nullTerminatorIndex - offset;
                    entries.Add(new Entry(entry.Tag, entry.Type, Encoding.UTF8.GetString(store, entry.Offset, length)));
                }
                else if (entry.Type is EntryType.StringArray)
                {
                    int offset = entry.Offset;
                    string[] strings = new string[entry.Count];
                    for (int i = 0; i < entry.Count; i++)
                    {
                        int nullTerminatorIndex = Array.IndexOf(store, (byte)0, offset);
                        if (nullTerminatorIndex == -1)
                        {
                            throw new InvalidOperationException("Invalid RPM header entry string (no null terminator found)");
                        }
                        int length = nullTerminatorIndex - offset;
                        strings[i] = Encoding.UTF8.GetString(store, offset, length);
                        offset += length + 1;
                    }
                    entries.Add(new Entry(entry.Tag, entry.Type, strings));
                }
                else
                {
                    Array contents = Array.CreateInstance(entry.Type switch
                    {
                        EntryType.Char => typeof(char),
                        EntryType.Int8 => typeof(byte),
                        EntryType.Int16 => typeof(short),
                        EntryType.Int32 => typeof(int),
                        EntryType.Int64 => typeof(long),
                        _ => throw new InvalidOperationException("Invalid RPM header entry type")
                    }, entry.Count);

                    int offset = entry.Offset;

                    for (int i = 0; i < entry.Count; i++)
                    {
                        switch (entry.Type)
                        {
                            case EntryType.Char:
                                contents.SetValue((char)store[offset], i);
                                offset += 1;
                                break;
                            case EntryType.Int8:
                                contents.SetValue(store[offset], i);
                                offset += 1;
                                break;
                            case EntryType.Int16:
                                contents.SetValue(BinaryPrimitives.ReadInt16BigEndian(store.AsSpan().Slice(offset.AlignUp(2), 2)), i);
                                offset += 2;
                                break;

                            case EntryType.Int32:
                                contents.SetValue(BinaryPrimitives.ReadInt32BigEndian(store.AsSpan().Slice(offset.AlignUp(4), 4)), i);
                                offset += 4;
                                break;

                            case EntryType.Int64:
                                contents.SetValue(BinaryPrimitives.ReadInt64BigEndian(store.AsSpan().Slice(offset.AlignUp(8), 8)), i);
                                offset += 8;
                                break;
                        }
                    }

                    entries.Add(new Entry(entry.Tag, entry.Type, contents));
                }
            }

            return new RpmHeader<TEntryTag>(entries);
        }

        public void WriteTo(Stream stream, TEntryTag immutableRegionTag)
        {
            stream.Write([0x8e, 0xad, 0xe8]); // magic
            stream.WriteByte(1); // version
            stream.Write([0, 0, 0, 0]); // reserved
            byte[] numIndexEntries = new byte[4];
            // Add 1 for the immutable region tag
            BinaryPrimitives.WriteInt32BigEndian(numIndexEntries, Entries.Count + 1);
            stream.Write(numIndexEntries);

            using MemoryStream storeStream = new();

            byte[] indexInfoBytes = new byte[16];
            IndexEntry immutableRegionIndexEntry = new()
            {
                Tag = Convert.ToInt32(immutableRegionTag),
                Type = EntryType.Binary,
                Offset = 0,
                Count = 16
            };

            WriteIndexEntry(indexInfoBytes, immutableRegionIndexEntry);
            stream.Write(indexInfoBytes);

            IndexEntry immutableRegionData = new()
            {
                Tag = Convert.ToInt32(immutableRegionTag),
                Type = EntryType.Binary,
                Offset = -(Entries.Count + 1) * Unsafe.SizeOf<IndexEntry>(),
                Count = 16
            };

            WriteIndexEntry(indexInfoBytes, immutableRegionData);
            storeStream.Write(indexInfoBytes);

            foreach (var entry in Entries)
            {
                BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes, Convert.ToInt32(entry.Tag));
                BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(4), (int)entry.Type);
                if (entry.Type is EntryType.Binary)
                {
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(8), (int)storeStream.Length);

                    ArraySegment<byte> binary = (ArraySegment<byte>)entry.Value;
                    storeStream.Write(binary.Array!, binary.Offset, binary.Count);
                    
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(12), binary.Count);
                }
                else if (entry.Type is EntryType.Null)
                {
                    throw new InvalidOperationException("Null entry should not be present in RPM header");
                }
                else if (entry.Type is EntryType.String or EntryType.I18NString)
                {
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(8), (int)storeStream.Length);
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(12), 1);

                    byte[] bytes = Encoding.UTF8.GetBytes((string)entry.Value);
                    storeStream.Write(bytes);
                    storeStream.WriteByte(0);

                }
                else if (entry.Type is EntryType.StringArray)
                {
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(8), (int)storeStream.Length);

                    string[] strings = (string[])entry.Value;

                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(12), strings.Length);

                    foreach (string str in strings)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(str);
                        storeStream.Write(bytes);
                        storeStream.WriteByte(0);
                    }
                }
                else
                {
                    int alignment = entry.Type switch
                    {
                        EntryType.Char => 1,
                        EntryType.Int8 => 1,
                        EntryType.Int16 => 2,
                        EntryType.Int32 => 4,
                        EntryType.Int64 => 8,
                        _ => throw new InvalidOperationException("Invalid RPM header entry type")
                    };

                    storeStream.AlignWriteTo(alignment);
                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(8), (int)storeStream.Length);

                    Array contents = (Array)entry.Value;

                    BinaryPrimitives.WriteInt32BigEndian(indexInfoBytes.AsSpan(12), contents.Length);

                    byte[] tempBeBytes = new byte[8];
                    for (int i = 0; i < contents.Length; i++)
                    {
                        switch (entry.Type)
                        {
                            case EntryType.Char:
                                storeStream.WriteByte((byte)(char)contents.GetValue(i)!);
                                break;
                            case EntryType.Int8:
                                storeStream.WriteByte((byte)contents.GetValue(i)!);
                                break;
                            case EntryType.Int16:
                                BinaryPrimitives.WriteInt16BigEndian(tempBeBytes, (short)contents.GetValue(i)!);
                                storeStream.Write(tempBeBytes, 0, 2);
                                break;
                            case EntryType.Int32:
                                BinaryPrimitives.WriteInt32BigEndian(tempBeBytes, (int)contents.GetValue(i)!);
                                storeStream.Write(tempBeBytes, 0, 4);
                                break;
                            case EntryType.Int64:
                                BinaryPrimitives.WriteInt64BigEndian(tempBeBytes, (long)contents.GetValue(i)!);
                                storeStream.Write(tempBeBytes, 0, 8);
                                break;
                        }
                    }
                }

                stream.Write(indexInfoBytes);
            }

            storeStream.CopyTo(stream);
        }
    }
}

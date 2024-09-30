// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Installers
{
    internal sealed class CpioEntry(ulong inode, string name, ulong timestamp, ulong ownerID, ulong groupID, uint mode, ushort numberOfLinks, ulong devMajor, ulong devMinor, ulong rdevMajor, ulong rdevMinor, MemoryStream dataStream)
    {
        public ulong Inode { get; } = inode;
        public string Name { get; } = name;
        public ulong Timestamp { get; } = timestamp;
        public ulong OwnerID { get; } = ownerID;
        public ulong GroupID { get; } = groupID;
        public uint Mode { get; } = mode;
        public Stream DataStream { get; } = dataStream;
        public ushort NumberOfLinks { get; } = numberOfLinks;
        public ulong DevMajor { get; } = devMajor;
        public ulong DevMinor { get; } = devMinor;
        public ulong RDevMajor { get; } = rdevMajor;
        public ulong RDevMinor { get; } = rdevMinor;
    }
}
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
    internal class RpmReader(RpmLead lead, RpmHeader signature, RpmHeader header, MemoryStream archiveStream) : IDisposable
    {
        public RpmLead Lead { get; } = lead;
        public RpmHeader Signature { get; } = signature;
        public RpmHeader Header { get; } = header;
        public MemoryStream ArchiveStream { get; } = archiveStream;

        public static unsafe RpmReader Read(Stream stream)
        {
            byte[] leadBytes = new byte[sizeof(RpmLead)];
            StreamHelpers.ReadExactly(stream, leadBytes, 0, sizeof(RpmLead));
            RpmLead lead = Unsafe.As<byte, RpmLead>(ref leadBytes[0]);
            if (lead.Magic[0] != 0xad || lead.Magic[1] != 0xeb || lead.Magic[2] != 0xdd || lead.Magic[3] != 0xee)
            {
                throw new InvalidDataException("Invalid RPM magic");
            }

            RpmHeader signature = RpmHeader.Read(stream);
            stream.AlignTo(8);
            RpmHeader header = RpmHeader.Read(stream);

            using GZipStream gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            MemoryStream archiveStream = new MemoryStream();
            gzipStream.CopyTo(archiveStream);
            archiveStream.Position = 0;
            return new RpmReader(lead, signature, header, archiveStream);
        }

        public void Dispose() => ArchiveStream.Dispose();
    }
}

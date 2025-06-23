// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;

namespace Microsoft.DotNet.SignTool
{
    internal sealed class ZipDataEntry : IDisposable
    {
        Stream _stream;
        ImmutableArray<byte> _contentHash;

        public ZipDataEntry(Stream contentStream, long contentSize)
        {
            if (contentStream.CanSeek)
            {
                _stream = contentStream;
            }
            else
            {
                // if we can't seek we need to copy the stream into a (seekable) MemoryStream so we can compute the content hash
                _stream = new MemoryStream((int)contentSize);
                contentStream.CopyTo(_stream);
            }

            // compute content hash and reset position back
            _contentHash = ContentUtil.GetContentHash(_stream);
            _stream.Position = 0;
        }

        public ZipDataEntry(ZipArchiveEntry zipArchiveEntry)
        {
            // the stream returned by zipArchiveEntry.Open() isn't seekable,
            // we can avoid creating a MemoryStream copy by just opening a separate instance for computing the content hash
            using var contentHashStream = zipArchiveEntry.Open();
            _contentHash = ContentUtil.GetContentHash(contentHashStream);

            // this will already be at position 0
            _stream = zipArchiveEntry.Open();
        }
        public ImmutableArray<byte> ContentHash => _contentHash;

        public void WriteToFile(string path)
        {
            using var fs = File.Create(path);
            _stream.CopyTo(fs);
        }

        public void Dispose()
        {
            _stream.Dispose();
            _stream = null;
        }
    }
}

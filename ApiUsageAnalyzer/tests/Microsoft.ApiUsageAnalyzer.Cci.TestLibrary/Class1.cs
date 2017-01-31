using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.ApiUsageAnalyzer.Cci.TestLibrary
{
    public class Inheritor : Stream
    {
        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length { get; }
        public override long Position { get; set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            using (var ms = new MemoryStream())
            {
                ms.Position = 5;
            }
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public void Foo(List<UnmanagedMemoryStream> l)
        {
            throw new InvalidOperationException();
        }
    }

    public class Crazy : System.Text.RegularExpressions.Regex
    {
        public Crazy()
        {
            this.capsize = 2;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Fx.Csv
{
    public class CsvTextReader : CsvReader
    {
        private CsvLineReader _reader;
        private IEnumerator<IEnumerable<string>> _enumerator;

        public CsvTextReader(TextReader textReader, CsvSettings settings)
            : base(settings)
        {
            _reader = new CsvLineReader(textReader, Settings);
            _enumerator = _reader.GetEnumerator();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _reader.Dispose();
        }

        public override IEnumerable<string> Read()
        {
            if (!_enumerator.MoveNext())
                return null;

            var line = _enumerator.Current;
            return line;
        }
    }
}
